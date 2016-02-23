// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
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
	public enum MongoDbSelector {
		MainDatabase,
		ProjectDatabase,
	}

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

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName, Expression<Func<TDocument, bool>> filter)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<TDocument> collection = db.GetCollection<TDocument>(collectionName);
			IAsyncCursor<TDocument> result = collection.Find<TDocument>(filter).ToCursor();
			return result.AsEnumerable();
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			return GetRecords<TDocument>(project, collectionName, _ => true);
		}

		public MongoProjectRecord GetProjectRecord(ILfProject project)
		{
			IMongoDatabase db = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = db.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			return collection.Find(proj => proj.ProjectCode == project.LfProjectCode)
				.Limit(1).FirstOrDefault();
		}

		public Dictionary<string, LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			MongoProjectRecord projectRecord = GetProjectRecord(project);
			if (projectRecord == null)
				return new Dictionary<string, LfInputSystemRecord>();
			return projectRecord.InputSystems;
		}

		public bool UpdateProjectRecord(ILfProject project, MongoProjectRecord projectRecord)
		{
			var filterBuilder = new FilterDefinitionBuilder<MongoProjectRecord>();
			FilterDefinition<MongoProjectRecord> filter = filterBuilder.Eq(record => record.ProjectCode, projectRecord.ProjectCode);
			return UpdateRecordImpl<MongoProjectRecord>(project, projectRecord, filter, MagicStrings.LfCollectionNameForProjectRecords, MongoDbSelector.MainDatabase);
		}

		/// <summary>
		/// Sets the input systems (writing systems) in the project configuration.
		/// During an initial clone, some vernacular and analysis input systems are also set.
		/// </summary>
		/// <returns>True if mongodb was updated</returns>
		/// <param name="project">Language forge Project.</param>
		/// <param name="inputSystems">List of input systems to add to the project configuration.</param>
		/// <param name="initialClone">If set to <c>true</c>, also update associated field view input systems. Default false</param>
		/// <param name="vernacularWs">Default vernacular writing system. Default blank</param>
		/// <param name="analysisWs">Default analysis writing system. Default blank</param>
		public bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			bool initialClone = false, string vernacularWs = "", string analysisWs = "")
		{
			UpdateDefinition<MongoProjectRecord> update = Builders<MongoProjectRecord>.Update.Set(rec => rec.InputSystems, inputSystems);
			FilterDefinition<MongoProjectRecord> filter = Builders<MongoProjectRecord>.Filter.Eq(record => record.ProjectCode, project.LfProjectCode);

			IMongoDatabase mongoDb = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = mongoDb.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			var updateOptions = new FindOneAndUpdateOptions<MongoProjectRecord> {
				IsUpsert = false // If there's no project record, we do NOT want to create one. That should have been done before SetInputSystems() is ever called.
			};
			collection.FindOneAndUpdate(filter, update, updateOptions);

			// For initial clone, also update field writing systems accordingly
			if (initialClone)
			{
				var builder = Builders<MongoProjectRecord>.Update;
				var updates = new List<UpdateDefinition<MongoProjectRecord>>();

				var vernacularInputSystems = new List<string> { vernacularWs };
				List<string> vernacularFieldsWsList = new List<string> {
					"citationForm", "lexeme"
				};
				foreach (var vernacularFieldName in vernacularFieldsWsList)
				{
					updates.Add(builder.Set("config.entry.fields." + vernacularFieldName + ".inputSystems", vernacularInputSystems));
					// This one won't compile: updates.Add(builder.Set(record => record.Config.Entry.Fields[vernacularFieldName].InputSystems, vernacularInputSystems));
					// Mongo can't handle this one: updates.Add(builder.Set(record => ((LfConfigMultiText)record.Config.Entry.Fields[vernacularFieldName]).InputSystems, vernacularInputSystems));
				}

				// TODO: sort out what LF fields are "vernacular" / "analysis"
				var analysisInputSystems = new List<string> { analysisWs };
				List<string> analysisFieldsWsList = new List<string> {
					"note", "senses.fields.examples.fields.sentence"
				};
				foreach (var analysisFieldName in analysisFieldsWsList)
				{
					updates.Add(builder.Set("config.entry.fields." + analysisFieldName + ".inputSystems", analysisInputSystems));
					// This one won't compile: updates.Add(builder.Set(record => record.Config.Entry.Fields[analysisFieldName].InputSystems, analysisInputSystems));
					// Mongo can't handle this one: updates.Add(builder.Set(record => ((LfConfigMultiText)record.Config.Entry.Fields[analysisFieldName]).InputSystems, analysisInputSystems));
				}

				// Also update the LF language code with the vernacular WS
				updates.Add(builder.Set("languageCode", vernacularWs));

				update = builder.Combine(updates);

				Logger.Debug("Built an input systems update that looks like {0}",
					update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
				collection.FindOneAndUpdate(filter, update, updateOptions);
			}

			return true;
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

		public bool UpdateRecord<TDocument>(ILfProject project, TDocument data, Guid guid, string collectionName, MongoDbSelector whichDb = MongoDbSelector.ProjectDatabase)
		{
			var filterBuilder = new FilterDefinitionBuilder<TDocument>();
			FilterDefinition<TDocument> filter = filterBuilder.Eq("guid", guid.ToString());
			bool result = UpdateRecordImpl(project, data, filter, collectionName, whichDb);
			Logger.Notice("Done saving {0} {1} into Mongo DB", typeof(TDocument), guid);
			return result;
		}

		public bool UpdateRecord(ILfProject project, LfLexEntry data)
		{
			var filterBuilder = Builders<LfLexEntry>.Filter;
			FilterDefinition<LfLexEntry> filter = filterBuilder.Eq(entry => entry.Guid, data.Guid);
			UpdateDefinition<LfLexEntry> coreUpdate = BuildUpdate(data);
			var doNotUpsert = new FindOneAndUpdateOptions<LfLexEntry> {
				IsUpsert = false,
				ReturnDocument = ReturnDocument.Before
			};
			var upsert = new FindOneAndUpdateOptions<LfLexEntry> {
				IsUpsert = true,
				ReturnDocument = ReturnDocument.After
			};
			// Special handling for LfLexEntry records: we need to decrement dirtySR iff it's >0, but Mongo
			// doesn't allow an update like "{'$inc': {dirtySR: -1}, '$max': {dirtySR: 0}}". We have to use
			// filters for that, and that means there are THREE possibilities:
			// 1. This is an *insert*, where dirtySR should be set to 0.
			// 2. This is an *update*, but dirtySR was already 0; do not decrement.
			// 3. This is an *update*, and dirtySR was >0; decrement.
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfLexEntry> coll = db.GetCollection<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon);
			LfLexEntry updateResult;
			if (coll.Count(filter) == 0)
			{
				// Theoretically, we could just do an InsertOne() here. But we have special logic in the
				// BuildUpdate() function (for handling Nullable<Guid> fields, for example), and we want to
				// make sure that gets applied for both inserts and updates. So we'll do an upsert even though
				// we *know* there's no previous data
				updateResult = coll.FindOneAndUpdate(filter, coreUpdate, upsert);
				return (updateResult != null);
			}
			else
			{
				var updateBuilder = Builders<LfLexEntry>.Update;
				var oneOrMoreFilter = filterBuilder.And(filter, filterBuilder.Gt(entry => entry.DirtySR, 0));
				var zeroOrLessFilter = filterBuilder.And(filter, filterBuilder.Lte(entry => entry.DirtySR, 0));
				var decrementUpdate = updateBuilder.Combine(coreUpdate, updateBuilder.Inc(item => item.DirtySR, -1));
				var noDecrementUpdate = updateBuilder.Combine(coreUpdate, updateBuilder.Max(item => item.DirtySR, 0));
				// Precisely one of the next two calls can succeed.
				try
				{
					updateResult = coll.FindOneAndUpdate(zeroOrLessFilter, noDecrementUpdate, doNotUpsert);
					if (updateResult != null)
						return true;
				}
				catch (MongoCommandException e)
				{
					// Max needs MongoDB version 2.6+. which may not be currently installed.
					// Decrementing DirtySR isn't needed for LfMerge v1.1
					Logger.Error("{0}: Possibly need to upgrade MongoDB to 2.6+", e);
				}
				updateResult = coll.FindOneAndUpdate(oneOrMoreFilter, decrementUpdate, doNotUpsert);
				return (updateResult != null);
			}
		}

		public bool UpdateRecord(ILfProject project, LfOptionList data, ObjectId id)
		{
			var filterBuilder = Builders<LfOptionList>.Filter;
			FilterDefinition<LfOptionList> filter = filterBuilder.Eq(optionList => optionList.Id, id);
			bool result = UpdateRecordImpl(project, data, filter, MagicStrings.LfCollectionNameForOptionLists, MongoDbSelector.ProjectDatabase);
			Logger.Notice("Done saving {0} with ObjectID {1} into Mongo DB", typeof(LfOptionList), id);
			return result;
		}

		private bool UpdateRecordImpl<TDocument>(ILfProject project, TDocument data, FilterDefinition<TDocument> filter, string collectionName, MongoDbSelector whichDb)
		{
			IMongoDatabase mongoDb;
			if (whichDb == MongoDbSelector.ProjectDatabase)
				mongoDb = GetProjectDatabase(project);
			else
				mongoDb = GetMainDatabase();
			UpdateDefinition<TDocument> update = BuildUpdate(data);
			IMongoCollection<TDocument> collection = mongoDb.GetCollection<TDocument>(collectionName);
			var updateOptions = new FindOneAndUpdateOptions<TDocument> {
				IsUpsert = true
			};
			collection.FindOneAndUpdate(filter, update, updateOptions);
			return true;
		}
	}
}

