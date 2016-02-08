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

		public IEnumerable<LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			IMongoDatabase db = GetMainDatabase();
			IMongoCollection<LfProject> collection = db.GetCollection<LfProject>(MagicStrings.LfCollectionNameForProjectRecords);
			IAsyncCursor<LfProject> result = collection.Find<LfProject>(projRecord => projRecord.ProjectCode == project.LfProjectCode).ToCursor();
			LfProject foundProject = result.FirstOrDefault();
			if (foundProject == null)
				return new List<LfInputSystemRecord>();
			return foundProject.InputSystems;
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
					updates.Add(builder.Set(prop.Name, (LfMultiText)prop.GetValue(doc)));
					break;
				case "LfStringArrayField":
					updates.Add(builder.Set(prop.Name, (LfStringArrayField)prop.GetValue(doc)));
					break;
				case "LfStringField":
					updates.Add(builder.Set(prop.Name, (LfStringField)prop.GetValue(doc)));
					break;
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
			// TODO: This "update this document in this MongoDB collection" code was moved from UpdateMongoDbFromFdoAction. Fix it up so it works.
			IMongoDatabase mongoDb = GetProjectDatabase(project); // TODO: If this is slow, might want to cache it in the instance
			var filterBuilder = new FilterDefinitionBuilder<TDocument>();
			UpdateDefinition<TDocument> update = BuildUpdate(data);
			FilterDefinition<TDocument> filter = filterBuilder.Eq("guid", guid.ToString());
			IMongoCollection<TDocument> collection = mongoDb.GetCollection<TDocument>(collectionName); // This was hardcoded to "lexicon" in the UpdateMongoDbFromFdoAction version
			//Logger.Notice("About to save {0} {1}", typeof(TDocument), guid);
			//Logger.Debug("Built filter that looks like: {0}", filter.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			//Logger.Debug("Built update that looks like: {0}", update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			// NOTE: Throwing away result of FindOneAnd___Async on purpose.
			var updateOptions = new FindOneAndUpdateOptions<TDocument> {
				IsUpsert = true
			};
			var ignored = collection.FindOneAndUpdateAsync(filter, update, updateOptions).Result; // Use this one to update fields within the entry. I think this one is preferred.
			Logger.Notice("Done saving {0} {1} into Mongo DB {2}", typeof(TDocument), guid, mongoDb.DatabaseNamespace.DatabaseName);

			return true;
		}

		public bool UpdateRecord<TDocument>(ILfProject project, TDocument data, ObjectId id, string collectionName)
		{
			// TODO: This "update this document in this MongoDB collection" code was moved from UpdateMongoDbFromFdoAction. Fix it up so it works.
			IMongoDatabase mongoDb = GetProjectDatabase(project); // TODO: If this is slow, might want to cache it in the instance
			var filterBuilder = new FilterDefinitionBuilder<TDocument>();
			UpdateDefinition<TDocument> update = BuildUpdate(data);
			FilterDefinition<TDocument> filter = filterBuilder.Eq("_id", id);
			IMongoCollection<TDocument> collection = mongoDb.GetCollection<TDocument>(collectionName); // This was hardcoded to "lexicon" in the UpdateMongoDbFromFdoAction version
			Console.WriteLine("About to save {0} with ObjectID {1}", typeof(TDocument), id);
			Console.WriteLine("Built filter that looks like: {0}", filter.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			Console.WriteLine("Built update that looks like: {0}", update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
			// NOTE: Throwing away result of FindOneAnd___Async on purpose.
			//var ignored = collection.FindOneAndReplaceAsync(filter, data).Result;  // Use this one to replace the WHOLE entry wholesale
			var ignored = collection.FindOneAndUpdateAsync(filter, update).Result; // Use this one to update fields within the entry. I think this one is preferred.
			Console.WriteLine("Done saving {0} with ObjectID {1} into Mongo DB {2}", typeof(TDocument), id, mongoDb.DatabaseNamespace.DatabaseName);

			return true;
		}
	}
}

