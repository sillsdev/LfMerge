// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;

using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using LfMerge.Settings;

namespace LfMerge.MongoConnector
{
	public class MongoConnection : IMongoConnection
	{
		private string connectionString;
		private string mainDatabaseName;
		private Lazy<IMongoClient> client;
		private ILogger _logger;
		private LfMergeSettingsIni _settings;

		public ILogger Logger { get { return _logger; } }
		public LfMergeSettingsIni Settings { get { return _settings; } }

		// TODO: Get rid of these hardcoded defaults and see what breaks, then make sure that code calls Initialize() like it should.
		public static string MainDatabaseName = "scriptureforge";
		public static string HostNameAndPort = "localhost:27017";

		public static void Initialize(string hostName = null, string mainDatabaseName = null)
		{
			if (hostName != null) HostNameAndPort = hostName;
			if (mainDatabaseName != null) MainDatabaseName = mainDatabaseName;

			// Serialize Boolean values permissively
			BsonSerializer.RegisterSerializationProvider(new BooleanSerializationProvider());

			// Use CamelCaseName conversions between Mongo and our mapping classes
			var pack = new ConventionPack();
			pack.Add(new CamelCaseElementNameConvention());
			ConventionRegistry.Register(
				"My Custom Conventions",
				pack,
				t => t.FullName.StartsWith("LfMerge."));

			// Register class mappings before opening first connection
			new MongoRegistrarForLfConfig().RegisterClassMappings();
			//new MongoRegistrarForLfFields().RegisterClassMappings();
		}

		public MongoConnection(LfMergeSettingsIni settings, ILogger logger)
		{
			_settings = settings;
			_logger = logger;
			connectionString = String.Format("mongodb://{0}", Settings.MongoDbHostNameAndPort);
			mainDatabaseName = Settings.MongoMainDatabaseName;
			client = new Lazy<IMongoClient>(GetNewConnection);
		}

		private MongoClient GetNewConnection()
		{
			return new MongoClient(connectionString);
		}

		private IMongoDatabase GetDatabase(string databaseName) {
			return client.Value.GetDatabase(databaseName);
		}

		public IMongoDatabase GetProjectDatabase(ILfProject project) {
			return GetDatabase(project.MongoDatabaseName);
		}

		public IMongoDatabase GetMainDatabase() {
			return GetDatabase(mainDatabaseName);
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<TDocument> collection = db.GetCollection<TDocument>(collectionName);
			IAsyncCursor<TDocument> result = collection.Find<TDocument>(_ => true).ToCursor();
			return result.AsEnumerable();
		}

		public Dictionary<string, LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			IMongoDatabase db = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = db.GetCollection<MongoProjectRecord>(MongoProjectRecord.ProjectsCollectionName);
			MongoProjectRecord record =
				collection.Find(proj => proj.ProjectCode == project.LfProjectCode)
					.Limit(1).FirstOrDefault();

			if (record == null)
				return new Dictionary<string, LfInputSystemRecord>();
			return record.InputSystems;
		}

		public bool SetInputSystems<TDocument>(ILfProject project, TDocument inputSystems)
		{
			return false;
		}

		private UpdateDefinition<TDocument> BuildUpdate<TDocument>(TDocument doc) {
			var builder = Builders<TDocument>.Update;
			var updates = new List<UpdateDefinition<TDocument>>();
			foreach (PropertyInfo prop in typeof(TDocument).GetProperties())
			{
				if (prop.PropertyType == typeof(MongoDB.Bson.ObjectId))
					continue; // Mongo doesn't allow changing Mongo IDs
				if (prop.GetCustomAttributes<BsonElementAttribute>().Any(attr => attr.ElementName == "id"))
					continue; // Don't change Languageforge-internal IDs either
				if (prop.GetValue(doc) == null)
				{
					updates.Add(builder.Unset(prop.Name));
					continue;
				}
				switch (prop.PropertyType.Name)
				{
				case "BsonDocument":
					updates.Add(builder.Set(prop.Name, (BsonDocument)prop.GetValue(doc)));
					break;
				case "Guid":
					updates.Add(builder.Set(prop.Name, ((Guid)prop.GetValue(doc)).ToString()));
					break;
				case "Nullable`1":
					if (prop.Name.Contains("Guid"))
						updates.Add(builder.Set(prop.Name, ((Guid)prop.GetValue(doc)).ToString()));
					else
						updates.Add(builder.Set(prop.Name, prop.GetValue(doc)));
					break;
				case "LfAuthorInfo":
					updates.Add(builder.Set(prop.Name, (LfAuthorInfo)prop.GetValue(doc)));
					break;
				case "LfMultiText":
					{
						LfMultiText value = (LfMultiText)prop.GetValue(doc);
						if (value.IsEmpty)
							updates.Add(builder.Unset(prop.Name));
						else
							updates.Add(builder.Set(prop.Name, value));
						break;
					}
				case "LfStringArrayField":
					{
						LfStringArrayField value = (LfStringArrayField)prop.GetValue(doc);
						if (value.IsEmpty)
							updates.Add(builder.Unset(prop.Name));
						else
							updates.Add(builder.Set(prop.Name, value));
						break;
					}
				case "LfStringField":
					{
						LfStringField value = (LfStringField)prop.GetValue(doc);
						if (value.IsEmpty)
							updates.Add(builder.Unset(prop.Name));
						else
							updates.Add(builder.Set(prop.Name, value));
						break;
					}
				case "List`1":
					switch (prop.PropertyType.GenericTypeArguments[0].Name)
					{
					case "LfSense":
						updates.Add(builder.Set(prop.Name, (List<LfSense>)prop.GetValue(doc)));
						break;
					case "LfExample":
						updates.Add(builder.Set(prop.Name, (List<LfExample>)prop.GetValue(doc)));
						break;
					case "LfPicture":
						updates.Add(builder.Set(prop.Name, (List<LfPicture>)prop.GetValue(doc)));
						break;
					case "LfOptionListItem":
						updates.Add(builder.Set(prop.Name, (List<LfOptionListItem>)prop.GetValue(doc)));
						break;
					default:
						updates.Add(builder.Set(prop.Name, (List<object>)prop.GetValue(doc)));
						break;
					}
					break;
				default:
					updates.Add(builder.Set(prop.Name, prop.GetValue(doc)));
					break;
				}
			}
			return builder.Combine(updates);
		}

		// TODO: These two UpdateRecord overloads share MOST of their code. Refactor to one method, called by
		// both of them with a different FilterDefinition.
		public bool UpdateRecord<TDocument>(ILfProject project, TDocument data, Guid guid, string collectionName)
		{
			var filterBuilder = new FilterDefinitionBuilder<TDocument>();
			FilterDefinition<TDocument> filter = filterBuilder.Eq("guid", guid.ToString());
			bool result = UpdateRecordImpl(project, data, filter, collectionName);
			Logger.Notice("Done saving {0} {1} into Mongo DB", typeof(TDocument), guid);
			return result;
		}

		public bool UpdateRecord<TDocument>(ILfProject project, TDocument data, ObjectId id, string collectionName)
		{
			var filterBuilder = new FilterDefinitionBuilder<TDocument>();
			FilterDefinition<TDocument> filter = filterBuilder.Eq("_id", id);
			bool result = UpdateRecordImpl(project, data, filter, collectionName);
			Logger.Notice("Done saving {0} with ObjectID {1} into Mongo DB", typeof(TDocument), id);
			return result;
		}

		private bool UpdateRecordImpl<TDocument>(ILfProject project, TDocument data, FilterDefinition<TDocument> filter, string collectionName)
		{
			IMongoDatabase mongoDb = GetProjectDatabase(project);
			UpdateDefinition<TDocument> update = BuildUpdate(data);
			IMongoCollection<TDocument> collection = mongoDb.GetCollection<TDocument>(collectionName);
			//Logger.Notice("About to save {0} with ObjectID {1}", typeof(TDocument), id);
			//Logger.Debug("Built filter that looks like: {0}", filter.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			//Logger.Debug("Built update that looks like: {0}", update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			collection.FindOneAndUpdate(filter, update);
			return true;
		}
	}
}

