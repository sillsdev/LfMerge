// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Concurrent;
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
		// Since calling GetDatabase() too often creates a new connection, we memoize the databases in this dictionary.
		// ConcurrentDictionary is used instead of Dictionary because it has a handy GetOrAdd() method.
		private ConcurrentDictionary<string, IMongoDatabase> dbs;
		private ILogger _logger;
		private LfMergeSettingsIni _settings;

		public ILogger Logger { get { return _logger; } }
		public LfMergeSettingsIni Settings { get { return _settings; } }

		// List of LF fields which will use vernacular or pronunciation input systems. Heirarchy is config.entry.fields...
		// We intentionally aren't setting custom example WS here, since it's a custom field with a custom name
		private readonly List<string> _vernacularWsFieldsList = new List<string> {
			"citationForm", "lexeme", "etymology", "senses.fields.examples.fields.sentence"
		};
		// Pronunciation fields are special: they want the *first* vernacular WS that uses IPA (or is otherwise flagged
		// as being a pronunciation writing system).
		private readonly List<string> _pronunciationWsFieldsList = new List<string> {
			"pronunciation"
		};
		// No need to create a similar list for analysis input systems, because those are the default.

		public static void Initialize()
		{
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
			dbs = new ConcurrentDictionary<string, IMongoDatabase>();
		}

		private MongoClient GetNewConnection()
		{
			return new MongoClient(connectionString);
		}

		private IMongoDatabase GetDatabase(string databaseName) {
			return dbs.GetOrAdd(databaseName, client.Value.GetDatabase(databaseName));
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
			using (IAsyncCursor<TDocument> cursor = collection.Find<TDocument>(filter).ToCursor())
			{
				while (cursor.MoveNext())
					foreach (TDocument doc in cursor.Current) // IAsyncCursor returns results in batches
						yield return doc;
			}
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			return GetRecords<TDocument>(project, collectionName, _ => true);
		}

		public LfOptionList GetLfOptionListByCode(ILfProject project, string listCode)
		{
			return GetRecords<LfOptionList>(project, MagicStrings.LfCollectionNameForOptionLists, list => list.Code == listCode).FirstOrDefault();
		}

		public MongoProjectRecord GetProjectRecord(ILfProject project)
		{
			IMongoDatabase db = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = db.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			return collection.Find(proj => proj.ProjectCode == project.ProjectCode)
				.Limit(1).FirstOrDefault();
		}

		private bool CanParseGuid(string guidStr)
		{
			Guid ignored;
			return Guid.TryParse(guidStr, out ignored);
		}

		public Dictionary<Guid, DateTime> GetAllModifiedDatesForEntries(ILfProject project)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<BsonDocument> lexicon = db.GetCollection<BsonDocument>(MagicStrings.LfCollectionNameForLexicon);
			var filter = new BsonDocument();
			filter.Add("guid", new BsonDocument("$ne", BsonNull.Value));
			filter.Add("dateModified", new BsonDocument("$ne", BsonNull.Value));
			var projection = new BsonDocument();
			projection.Add("guid", 1);
			projection.Add("dateModified", 1);
			Dictionary<Guid, DateTime> results =
				lexicon
				.Find(filter)
				.Project(projection)
				.ToEnumerable()
				.Where(doc => doc.Contains("guid") && CanParseGuid(doc.GetValue("guid").AsString) && doc.Contains("dateModified"))
				.ToDictionary(doc => Guid.Parse(doc.GetValue("guid").AsString),
				              doc => doc.GetValue("dateModified").AsBsonDateTime.ToUniversalTime());
			return results;
		}

		public Dictionary<string, LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			MongoProjectRecord projectRecord = GetProjectRecord(project);
			if (projectRecord == null)
				return new Dictionary<string, LfInputSystemRecord>();
			return projectRecord.InputSystems;
		}

		/// <summary>
		/// Get the config settings for all custom fields (and only custom fields).
		/// </summary>
		/// <returns>Dictionary of custom field settings, flattened so entry, sense and example custom fields are all at the dict's top level.</returns>
		/// <param name="project">LF Project.</param>
		public Dictionary<string, LfConfigFieldBase> GetCustomFieldConfig(ILfProject project)
		{
			var result = new Dictionary<string, LfConfigFieldBase>();
			MongoProjectRecord projectRecord = GetProjectRecord(project);
			if (projectRecord == null || projectRecord.Config == null)
				return result;
			LfConfigFieldList entryConfig = projectRecord.Config.Entry;
			LfConfigFieldList senseConfig = null;
			LfConfigFieldList exampleConfig = null;
			if (entryConfig != null && entryConfig.Fields.ContainsKey("senses"))
				senseConfig = entryConfig.Fields["senses"] as LfConfigFieldList;
			if (senseConfig != null && senseConfig.Fields.ContainsKey("examples"))
				exampleConfig = senseConfig.Fields["examples"] as LfConfigFieldList;
			foreach (LfConfigFieldList fieldList in new LfConfigFieldList[]{ entryConfig, senseConfig, exampleConfig })
				if (fieldList != null)
					foreach (KeyValuePair<string, LfConfigFieldBase> fieldInfo in fieldList.Fields)
						if (fieldInfo.Key.StartsWith("customField_"))
							result[fieldInfo.Key] = fieldInfo.Value;
			return result;
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
		/// <param name="vernacularWs">Default vernacular writing system. Default blank</param>
		/// <param name="analysisWs">Default analysis writing system. Default blank</param>
		public bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			List<string> vernacularWss, List<string> analysisWss, List<string> pronunciationWss)
		{
			UpdateDefinition<MongoProjectRecord> update = Builders<MongoProjectRecord>.Update.Set(rec => rec.InputSystems, inputSystems);
			FilterDefinition<MongoProjectRecord> filter = Builders<MongoProjectRecord>.Filter.Eq(record => record.ProjectCode, project.ProjectCode);

			IMongoDatabase mongoDb = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = mongoDb.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			var updateOptions = new FindOneAndUpdateOptions<MongoProjectRecord> {
				IsUpsert = false // If there's no project record, we do NOT want to create one. That should have been done before SetInputSystems() is ever called.
			};
			MongoProjectRecord oldProjectRecord = collection.FindOneAndUpdate(filter, update, updateOptions);

			// For initial clone, also update field writing systems accordingly
			if (project.IsInitialClone)
			{
				// Currently only MultiText fields have input systems in the config. If MultiParagraph fields acquire input systems as well,
				// we'll need to add those to the logic below.
				var analysisFields = new List<string>();
				IEnumerable<string> entryFields = oldProjectRecord.Config.Entry.Fields.Where(kv => kv.Value is LfConfigMultiText).Select(kv => kv.Key);
				foreach (string fieldName in entryFields)
				{
					if (!_vernacularWsFieldsList.Contains(fieldName) && !_pronunciationWsFieldsList.Contains(fieldName))
						analysisFields.Add(fieldName);
				}
				if (oldProjectRecord.Config.Entry.Fields.ContainsKey("senses"))
				{
					LfConfigFieldList senses = (LfConfigFieldList)oldProjectRecord.Config.Entry.Fields["senses"];
					IEnumerable<string> senseFields = senses.Fields.Where(kv => kv.Value is LfConfigMultiText).Select(kv => kv.Key);
					foreach (string fieldName in senseFields)
					{
						if (!_vernacularWsFieldsList.Contains(fieldName) && !_pronunciationWsFieldsList.Contains(fieldName))
							analysisFields.Add("senses.fields." + fieldName);
					}
					if (senses.Fields.ContainsKey("examples"))
					{
						LfConfigFieldList examples = (LfConfigFieldList)senses.Fields["examples"];
						IEnumerable<string> exampleFields = examples.Fields.Where(kv => kv.Value is LfConfigMultiText).Select(kv => kv.Key);
						foreach (string fieldName in exampleFields)
						{
							if (!_vernacularWsFieldsList.Contains(fieldName) && !_pronunciationWsFieldsList.Contains(fieldName))
								analysisFields.Add("senses.fields.examples.fields." + fieldName);
						}
					}
				}

				var builder = Builders<MongoProjectRecord>.Update;
				var updates = new List<UpdateDefinition<MongoProjectRecord>>();

				foreach (var vernacularFieldName in _vernacularWsFieldsList)
				{
					updates.Add(builder.Set("config.entry.fields." + vernacularFieldName + ".inputSystems", vernacularWss));
					// This one won't compile: updates.Add(builder.Set(record => record.Config.Entry.Fields[vernacularFieldName].InputSystems, vernacularWss));
					// Mongo can't handle this one: updates.Add(builder.Set(record => ((LfConfigMultiText)record.Config.Entry.Fields[vernacularFieldName]).InputSystems, vernacularWss));
				}

				// Pronunciation fields fall back on vernacular writing system if no pronunciation WS exists.
				if (pronunciationWss.Count == 0)
					pronunciationWss = vernacularWss;
				foreach (var pronunciationFieldName in _pronunciationWsFieldsList)
				{
					updates.Add(builder.Set("config.entry.fields." + pronunciationFieldName + ".inputSystems", pronunciationWss.Take(1)));  // Not First() since it still needs to be an enumerable
					// This one won't compile: updates.Add(builder.Set(record => record.Config.Entry.Fields[pronunciationFieldName].InputSystems, pronunciationWss));
					// Mongo can't handle this one: updates.Add(builder.Set(record => ((LfConfigMultiText)record.Config.Entry.Fields[pronunciationFieldName]).InputSystems, pronunciationWss));
				}

				foreach (var analysisFieldName in analysisFields)
				{
					updates.Add(builder.Set("config.entry.fields." + analysisFieldName + ".inputSystems", analysisWss));
					// This one won't compile: updates.Add(builder.Set(record => record.Config.Entry.Fields[analysisFieldName].InputSystems, analysisWss));
					// Mongo can't handle this one: updates.Add(builder.Set(record => ((LfConfigMultiText)record.Config.Entry.Fields[analysisFieldName]).InputSystems, analysisWss));
				}

				// Also update the LF language code with the vernacular WS
				updates.Add(builder.Set("languageCode", vernacularWss.First()));

				update = builder.Combine(updates);

//				Logger.Debug("Built an input systems update that looks like {0}",
//					update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
				collection.FindOneAndUpdate(filter, update, updateOptions);
			}

			return true;
		}

		/// <summary>
		/// Remove previous project custom field configurations that no longer exist,
		/// and then update them at the appropriate entry, senses, and examples level.
		/// Also adds these field names to the displayed fieldOrder.
		/// </summary>
		/// <param name="project">LF project</param>
		/// <param name="lfCustomFieldList"> Dictionary of LF custom field settings</param>
		/// <returns>True if mongodb was updated</returns>
		public bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
//			Logger.Debug("Setting {0} custom field setting(s)", lfCustomFieldList.Count());

			var builder = Builders<MongoProjectRecord>.Update;
			var previousUpdates = new List<UpdateDefinition<MongoProjectRecord>>();
			var currentUpdates = new List<UpdateDefinition<MongoProjectRecord>>();
			FilterDefinition<MongoProjectRecord> filter = Builders<MongoProjectRecord>.Filter.Eq(record => record.ProjectCode, project.ProjectCode);

			IMongoDatabase mongoDb = GetMainDatabase();
			IMongoCollection<MongoProjectRecord> collection = mongoDb.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			var updateOptions = new FindOneAndUpdateOptions<MongoProjectRecord> {
				IsUpsert = false // If there's no project record, we do NOT want to create one. That should have been done before SetCustomFieldConfig() is ever called.
			};

			List<string> entryCustomFieldOrder = new List<string>();
			List<string> senseCustomFieldOrder = new List<string>();
			List<string> exampleCustomFieldOrder = new List<string>();

			// Clean out previous fields and fieldOrders that no longer exist (removed from FDO)
			foreach (string customFieldNameToRemove in GetCustomFieldConfig(project).Keys.Except(lfCustomFieldList.Keys.ToList()))
			{
				if (customFieldNameToRemove.StartsWith(MagicStrings.LfCustomFieldEntryPrefix))
				{
					previousUpdates.Add(builder.Unset(String.Format("config.entry.fields.{0}", customFieldNameToRemove)));
					entryCustomFieldOrder.Add(customFieldNameToRemove);
				}
				else if (customFieldNameToRemove.StartsWith(MagicStrings.LfCustomFieldSensesPrefix))
				{
					previousUpdates.Add(builder.Unset(String.Format("config.entry.fields.senses.fields.{0}", customFieldNameToRemove)));
					senseCustomFieldOrder.Add(customFieldNameToRemove);
				}
				else if (customFieldNameToRemove.StartsWith(MagicStrings.LfCustomFieldExamplePrefix))
				{
					previousUpdates.Add(builder.Unset(String.Format("config.entry.fields.senses.fields.examples.fields.{0}", customFieldNameToRemove)));
					exampleCustomFieldOrder.Add(customFieldNameToRemove);
				}
			}
			if (entryCustomFieldOrder.Count > 0)
				previousUpdates.Add(builder.PullAll("config.entry.fieldOrder", entryCustomFieldOrder));
			if (senseCustomFieldOrder.Count > 0)
				previousUpdates.Add(builder.PullAll("config.entry.fields.senses.fieldOrder", senseCustomFieldOrder));
			if (exampleCustomFieldOrder.Count > 0)
				previousUpdates.Add(builder.PullAll("config.entry.fields.senses.fields.examples.fieldOrder", exampleCustomFieldOrder));
			var previousUpdate = builder.Combine(previousUpdates);

			// Now update the current fields and fieldOrders
			entryCustomFieldOrder = new List<string>();
			senseCustomFieldOrder = new List<string>();
			exampleCustomFieldOrder = new List<string>();
			foreach (var customFieldKVP in lfCustomFieldList)
			{
				if (customFieldKVP.Key.StartsWith(MagicStrings.LfCustomFieldEntryPrefix))
				{
					currentUpdates.Add(builder.Set(String.Format("config.entry.fields.{0}", customFieldKVP.Key), customFieldKVP.Value));
					entryCustomFieldOrder.Add(customFieldKVP.Key);
				}
				else if (customFieldKVP.Key.StartsWith(MagicStrings.LfCustomFieldSensesPrefix))
				{
					currentUpdates.Add(builder.Set(String.Format("config.entry.fields.senses.fields.{0}", customFieldKVP.Key), customFieldKVP.Value));
					senseCustomFieldOrder.Add(customFieldKVP.Key);
				}
				else if (customFieldKVP.Key.StartsWith(MagicStrings.LfCustomFieldExamplePrefix))
				{
					currentUpdates.Add(builder.Set(String.Format("config.entry.fields.senses.fields.examples.fields.{0}", customFieldKVP.Key), customFieldKVP.Value));
					exampleCustomFieldOrder.Add(customFieldKVP.Key);
				}
			}
			if (entryCustomFieldOrder.Count > 0)
				currentUpdates.Add(builder.AddToSetEach("config.entry.fieldOrder", entryCustomFieldOrder));
			if (senseCustomFieldOrder.Count > 0)
				currentUpdates.Add(builder.AddToSetEach("config.entry.fields.senses.fieldOrder", senseCustomFieldOrder));
			if (exampleCustomFieldOrder.Count > 0)
				currentUpdates.Add(builder.AddToSetEach("config.entry.fields.senses.fields.examples.fieldOrder", exampleCustomFieldOrder));
			var currentUpdate = builder.Combine(currentUpdates);

			// Because removing and updating fieldOrders involved the same field names,
			// we have to separate the Mongo operation into two updates
			try
			{
				if (previousUpdates.Count > 0)
					collection.FindOneAndUpdate(filter, previousUpdate, updateOptions);
				if (currentUpdates.Count > 0)
					collection.FindOneAndUpdate(filter, currentUpdate, updateOptions);
				return true;
			}
			catch (MongoCommandException e)
			{
				Logger.Error("Mongo exception writing custom fields: {0}", e);
				return false;
			}
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
				if (prop.Name == "DateCreated" && prop.PropertyType == typeof(DateTime) && (DateTime)prop.GetValue(doc) == default(DateTime))
				{
					// Refuse to reset DateCreated in Mongo to 0001-01-01, since that's NEVER correct
					continue;
				}
				if (prop.GetValue(doc) == null)
				{
					if (prop.Name == "DateCreated")
						continue; // Once DateCreated exists, it should never be unset
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
//			Logger.Debug("Done saving {0} {1} into Mongo DB", typeof(TDocument), guid);
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
				// Future version will use Max to decrement DirtySR.  MongoDB will need to be version 2.6+.
				// which may not be currently installed. Decrementing DirtySR isn't needed for LfMerge v1.1
				// var noDecrementUpdate = updateBuilder.Combine(coreUpdate, updateBuilder.Max(item => item.DirtySR, 0));
				var noDecrementUpdate = updateBuilder.Combine(coreUpdate, updateBuilder.Set(item => item.DirtySR, 0));
				// Precisely one of the next two calls can succeed.
				try
				{
					updateResult = coll.FindOneAndUpdate(zeroOrLessFilter, noDecrementUpdate, doNotUpsert);
					if (updateResult != null)
						return true;
				}
				catch (MongoCommandException e)
				{
					Logger.Error("{0}: Possibly need to upgrade MongoDB to 2.6+", e);
				}
				updateResult = coll.FindOneAndUpdate(oneOrMoreFilter, decrementUpdate, doNotUpsert);
				return (updateResult != null);
			}
		}

		public bool UpdateRecord(ILfProject project, LfOptionList data, string listCode)
		{
			var filterBuilder = Builders<LfOptionList>.Filter;
			FilterDefinition<LfOptionList> filter = filterBuilder.Eq(optionList => optionList.Code, listCode);
			bool result = UpdateRecordImpl(project, data, filter, MagicStrings.LfCollectionNameForOptionLists, MongoDbSelector.ProjectDatabase);
//			Logger.Debug("Done saving {0} with list code {1} into Mongo DB", typeof(LfOptionList), listCode);
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

		// Don't use this to remove LF entries.  Set IsDeleted field instead
		public bool RemoveRecord(ILfProject project, Guid guid)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfLexEntry> collection = db.GetCollection<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon);
			FilterDefinition<LfLexEntry> filter = Builders<LfLexEntry>.Filter.Eq(entry => entry.Guid, guid);
			var removeResult = collection.DeleteOne(filter);
			return (removeResult != null);
		}
	}
}

