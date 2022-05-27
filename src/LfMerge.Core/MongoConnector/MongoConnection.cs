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

using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;

namespace LfMerge.Core.MongoConnector
{
	public enum MongoDbSelector {
		MainDatabase,
		ProjectDatabase,
	}

	public class MongoConnection : IMongoConnection
	{
		private string mainDatabaseName;
		private Lazy<IMongoClient> client;
		// Since calling GetDatabase() too often creates a new connection, we memoize the databases in this dictionary.
		// ConcurrentDictionary is used instead of Dictionary because it has a handy GetOrAdd() method.
		private ConcurrentDictionary<string, IMongoDatabase> dbs;
		private ILogger _logger;
		private LfMergeSettings _settings;

		public ILogger Logger { get { return _logger; } }
		public LfMergeSettings Settings { get { return _settings; } }

		// List of LF fields which will use vernacular or pronunciation input systems. Hierarchy is config.entry.fields...
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

		public MongoConnection(LfMergeSettings settings, ILogger logger)
		{
			_settings = settings;
			_logger = logger;
			mainDatabaseName = Settings.MongoMainDatabaseName;
			client = new Lazy<IMongoClient>(GetNewConnection);
			dbs = new ConcurrentDictionary<string, IMongoDatabase>();
		}

		private MongoClient GetNewConnection()
		{
			var clientSettings = new MongoClientSettings();
			// clientSettings.WriteConcern = WriteConcern.WMajority; // If increasing the wait queue size still doesn't help, try this as well
			clientSettings.WaitQueueSize = 50000;
			clientSettings.Server = new MongoServerAddress(Settings.MongoHostname, Settings.MongoPort);
			return new MongoClient(clientSettings);
		}

		private IMongoDatabase GetDatabase(string databaseName) {
			return dbs.GetOrAdd(databaseName, dbName => client.Value.GetDatabase(dbName));
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

		public long LexEntryCount(ILfProject project)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfLexEntry> lexicon = db.GetCollection<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon);
			FilterDefinition<LfLexEntry> allEntries = Builders<LfLexEntry>.Filter.Empty;
			return lexicon.Count(allEntries);
		}

		public Dictionary<Guid, DateTime> GetAllModifiedDatesForEntries(ILfProject project)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<BsonDocument> lexicon = db.GetCollection<BsonDocument>(MagicStrings.LfCollectionNameForLexicon);
			var filter = new BsonDocument();
			filter.Add("guid", new BsonDocument("$ne", BsonNull.Value));
			filter.Add("authorInfo.modifiedDate", new BsonDocument("$ne", BsonNull.Value));
			var projection = new BsonDocument();
			projection.Add("guid", 1);
			projection.Add("authorInfo.modifiedDate", 1);
			Dictionary<Guid, DateTime> results =
				lexicon
				.Find(filter)
				.Project(projection)
				.ToEnumerable()
				.Where(doc => doc.Contains("guid") && CanParseGuid(doc.GetValue("guid").AsString)
					&& doc.Contains("authorInfo") && doc["authorInfo"].BsonType == BsonType.Document
					&& doc["authorInfo"].AsBsonDocument.Contains("modifiedDate")
					&& doc["authorInfo"].AsBsonDocument["modifiedDate"].BsonType == BsonType.DateTime)
				.ToDictionary(doc => Guid.Parse(doc.GetValue("guid").AsString),
				              doc => doc["authorInfo"].AsBsonDocument.GetValue("modifiedDate").AsBsonDateTime.ToUniversalTime());
			return results;
		}

		public Dictionary<Guid, ObjectId> GetObjectIdsByGuidForCollection(ILfProject project, string collectionName)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<BsonDocument> lexicon = db.GetCollection<BsonDocument>(collectionName);
			var filter = Builders<BsonDocument>.Filter.Empty;
			var projection = new BsonDocument("guid", 1);
			projection.Add("_id", 1);
			Dictionary<Guid, ObjectId> results =
				lexicon
				.Find(filter)
				.Project(projection)
				.ToEnumerable()
				.Where(doc => doc.Contains("guid") && CanParseGuid(doc.GetValue("guid").AsString))
				.ToDictionary(doc => Guid.Parse(doc.GetValue("guid").AsString),
				              doc => doc["_id"].AsObjectId);
			return results;
		}

		public Dictionary<ObjectId, Guid> GetGuidsByObjectIdForCollection(ILfProject project, string collectionName)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<BsonDocument> lexicon = db.GetCollection<BsonDocument>(collectionName);
			var filter = Builders<BsonDocument>.Filter.Empty;
			var projection = new BsonDocument("guid", 1);
			projection.Add("_id", 1);
			Dictionary<ObjectId, Guid> results =
				lexicon
				.Find(filter)
				.Project(projection)
				.ToEnumerable()
				.Where(doc => doc.Contains("guid") && CanParseGuid(doc.GetValue("guid").AsString))
				.ToDictionary(doc => doc["_id"].AsObjectId,
							  doc => Guid.Parse(doc.GetValue("guid").AsString));
			return results;
		}

		public IEnumerable<LfComment> GetComments(ILfProject project)
		{
			return GetRecords<LfComment>(project, MagicStrings.LfCollectionNameForLexiconComments);
		}

		public void UpdateComments(ILfProject project, List<LfComment> commentsFromFW)
		{
			// Design notes: We get comments with a Regarding.TargetGuid, which we need to turn into an EntryRef
			Dictionary<Guid, ObjectId> mongoIdsForEntries = GetObjectIdsByGuidForCollection(project, MagicStrings.LfCollectionNameForLexicon);
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfComment> collection = db.GetCollection<LfComment>(MagicStrings.LfCollectionNameForLexiconComments);
			var commentUpdates = new List<UpdateOneModel<LfComment>>(commentsFromFW.Count);
			var filterBuilder = Builders<LfComment>.Filter;
			var updateBuilder = Builders<LfComment>.Update;

			foreach (LfComment comment in commentsFromFW)
			{
				ObjectId mongoId;
				FilterDefinition<LfComment> filter;
				UpdateDefinition<LfComment> update;
				Guid targetGuid = Guid.Empty;
				DateTime utcNow = DateTime.UtcNow;
				if (comment.Regarding != null
					&& comment.Regarding.TargetGuid != null
					&& Guid.TryParse(comment.Regarding.TargetGuid, out targetGuid)
					&& mongoIdsForEntries.TryGetValue(targetGuid, out mongoId))
				{
					filter = filterBuilder.Eq(cmt => cmt.Guid, comment.Guid);
					update = updateBuilder
						.Set(c => c.AuthorNameAlternate, comment.AuthorNameAlternate)
						.Set(c => c.Content, comment.Content)
						.Set(c => c.EntryRef, mongoId)
						.Set(c => c.Guid, comment.Guid)
						// DateCreated and DateModified on the comment record track when that Mongo record was created.
						// AuthorInfo's CreatedDate and ModifiedDate track the values from FDO. (See comments in ConvertFdoToMongoLexicon for more details.)
						.SetOnInsert(c => c.DateCreated, utcNow)  // SetOnInsert because DateCreated should only be set once
						.Set(c => c.DateModified, utcNow)  // TODO: Can we somehow make this change only if anything else changed? Can we get Mongo to do that for us?
						.Set(c => c.AuthorInfo.CreatedDate, comment.DateCreated)
						.Set(c => c.AuthorInfo.ModifiedDate, comment.DateModified)
						// We do not set the user refs in AuthorInfo, nor do we change them
						.Set(c => c.Regarding, comment.Regarding)
						.Set(c => c.Replies, comment.Replies)
						.Set(c => c.IsDeleted, comment.IsDeleted)
						.Set(c => c.Status, comment.Status)
						.Set(c => c.StatusGuid, comment.StatusGuid)
						.Set(c => c.ContextGuid, comment.ContextGuid)
						;
					commentUpdates.Add(new UpdateOneModel<LfComment>(filter, update) { IsUpsert = true });
				}
				// If we couldn't look up the MongoId for this comment.Regarding field, we skip the comment entirely
			}
			var options = new BulkWriteOptions { IsOrdered = false };
			// Mongo doesn't like bulk updates with 0 items in them, and will throw an exception instead of sensibly doing nothing. So we have to protect it from itself.
			if (commentUpdates.Count > 0)
			{
				var result = collection.BulkWrite(commentUpdates, options);
			}
		}

		public void UpdateReplies(ILfProject project, List<Tuple<string, List<LfCommentReply>>> repliesFromFWWithCommentGuids)
		{
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfComment> collection = db.GetCollection<LfComment>(MagicStrings.LfCollectionNameForLexiconComments);
			List<UpdateOneModel<LfComment>> replyUpdates = new List<UpdateOneModel<LfComment>>();
			foreach (Tuple<string, List<LfCommentReply>> replyWithCommentGuid in repliesFromFWWithCommentGuids)
			{
				string commentGuidStr = replyWithCommentGuid.Item1;
				List<LfCommentReply> replies = replyWithCommentGuid.Item2;
				Guid commentGuid;
				if (Guid.TryParse(commentGuidStr, out commentGuid))
				{
					replyUpdates.Add(PrepareReplyUpdateForOneComment(commentGuid, replies));
				}
			}
			// Mongo doesn't like bulk updates with 0 items in them, and will throw an exception instead of sensibly doing nothing. So we have to protect it from itself.
			if (replyUpdates.Count > 0)
			{
				var options = new BulkWriteOptions { IsOrdered = false };
				var repliesResult = collection.BulkWrite(replyUpdates, options);   // TODO: Uncomment this once we get the Mongo query right.
			}
		}

		public void UpdateCommentStatuses(ILfProject project, List<KeyValuePair<string, Tuple<string, string>>> statusChanges)
		{
			Dictionary<Guid, ObjectId> mongoIdsForEntries = GetObjectIdsByGuidForCollection(project, MagicStrings.LfCollectionNameForLexicon);
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfComment> collection = db.GetCollection<LfComment>(MagicStrings.LfCollectionNameForLexiconComments);
			var commentUpdates = new List<UpdateOneModel<LfComment>>(statusChanges.Count);
			var filterBuilder = Builders<LfComment>.Filter;
			var updateBuilder = Builders<LfComment>.Update;
			DateTime utcNow = DateTime.UtcNow;

			foreach (KeyValuePair<string, Tuple<string, string>> kv in statusChanges)
			{
				Guid commentGuid = Guid.Parse(kv.Key);
				var newStatus = kv.Value.Item1;
				var newStatusGuid = Guid.Parse(kv.Value.Item2);
				FilterDefinition<LfComment> filter;
				UpdateDefinition<LfComment> update;

				filter = filterBuilder.Eq(cmt => cmt.Guid, commentGuid);
				update = updateBuilder
					.Set(c => c.DateModified, utcNow)  // So that IndexedDB picks up this change in status
					.Set(c => c.Status, newStatus)
					.Set(c => c.StatusGuid, newStatusGuid);
				commentUpdates.Add(new UpdateOneModel<LfComment>(filter, update) { IsUpsert = true });
			}
			var options = new BulkWriteOptions { IsOrdered = false };
			// Mongo doesn't like bulk updates with 0 items in them, and will throw an exception instead of sensibly doing nothing. So we have to protect it from itself.
			if (commentUpdates.Count > 0)
			{
				var result = collection.BulkWrite(commentUpdates, options);
			}
		}

		public UpdateOneModel<LfComment> PrepareReplyUpdateForOneComment(Guid commentGuid, List<LfCommentReply> replies)
		{
			foreach (LfCommentReply reply in replies)
			{
				// The LfCommentReply objects we receive here are coming from FW with a blank UniqId. We need to set the UniqId field so the JS code can do the right thing.
				DateTime utcNow = DateTime.UtcNow;  // We want a different timestamp for *each* reply
				reply.UniqId = PseudoPhp.UniqueIdFromDateTime(utcNow);
			}
			FilterDefinition<LfComment> filter = Builders<LfComment>.Filter.Eq(comment => comment.Guid, commentGuid);
			UpdateDefinition<LfComment> update = Builders<LfComment>.Update.PushEach(comment => comment.Replies, replies).Set(comment => comment.DateModified, DateTime.UtcNow);
			return new UpdateOneModel<LfComment>(filter, update);
		}

/* No longer using this one. Replaced by UpdateReplies above.
		public IEnumerable<UpdateOneModel<LfComment>> PrepareUpdateCommentReplies(LfComment commentDataFromFW, HashSet<Guid?> existingReplyGuids)
		{
			// NOTE: https://stackoverflow.com/q/26320673 suggests that this approach won't work: when there's no element match, it won't know where to insert the item.
			// So I'm going to have to come up with a different approach.
			if (false){
			foreach (LfCommentReply reply in commentDataFromFW.Replies)
			{
				if (reply.Guid == null) continue;
				var filter = Builders<LfComment>.Filter.ElemMatch(comment => comment.Replies, r => r.Guid == reply.Guid);
				DateTime utcNow = DateTime.UtcNow;
				var update = Builders<LfComment>.Update.SetOnInsert(comment => comment.Replies[-1].UniqId, PseudoPhp.NonUniqueIdFromDateTime(utcNow));
				update.SetOnInsert(comment => comment.Replies[-1].Guid, reply.Guid);
				update.SetOnInsert(comment => comment.Replies[-1].AuthorInfo.CreatedDate, reply.AuthorInfo.CreatedDate);
				// We only set the modified date on insert, so that if LF changes it, we won't overwrite that.
				update.SetOnInsert(comment => comment.Replies[-1].AuthorInfo.ModifiedDate, reply.AuthorInfo.ModifiedDate);
				update.SetOnInsert(comment => comment.Replies[-1].Content, reply.Content);  // TODO: What happens if someone edits it in LF? Are we going to overwrite it? If so, then Set rather than SetOnInsert.
				update.Set(comment => comment.Replies[-1].AuthorNameAlternate, reply.AuthorNameAlternate);
				update.Set(comment => comment.Replies[-1].IsDeleted, reply.IsDeleted);
				yield return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = true };
			}
			}

			Logger.Debug("Got existing reply guids: [{0}]", String.Join(", ", existingReplyGuids.Where(g => g != null).Select(g => g.ToString())));

			foreach (LfCommentReply reply in commentDataFromFW.Replies)
			{
				if (reply.Guid == null) continue;
				if (existingReplyGuids.Contains(reply.Guid))
				{
					// Build an update. TODO: Actually, what do we *do* with an updated LfCommentReply? Are we going to modify it at all?

					// TOCHECK: What happens if we return nothing at all
					// var filter = Builders<LfComment>.Filter.ElemMatch(comment => comment.Replies, r => r.Guid == reply.Guid);
					// var update = Builders<LfComment>.Update.SetOnInsert(comment => comment.Replies[-1].UniqId, PseudoPhp.NonUniqueIdFromDateTime(utcNow));
					// yield return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = false };
				}
				else
				{
					DateTime utcNow = DateTime.UtcNow;  // Need a *different* DateTime for each reply, so we put this inside the loop, not ouside
					var newReply = new LfCommentReply {
						Guid = reply.Guid,
						UniqId = PseudoPhp.NonUniqueIdFromDateTime(utcNow),
						Content = reply.Content,
						AuthorInfo = new LfAuthorInfo {
							CreatedDate = utcNow,
							ModifiedDate = utcNow,
						},
						AuthorNameAlternate = reply.AuthorNameAlternate,
						IsDeleted = reply.IsDeleted,
					};

					var filter = Builders<LfComment>.Filter.Eq(comment => comment.Guid, commentDataFromFW.Guid);
					var update = Builders<LfComment>.Update.Push(comment => comment.Replies, newReply);
					yield return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = false };
				}
			}
		}
 */
		// public IEnumerable<UpdateOneModel<LfComment>> PrepareUpdateCommentReplies(LfComment comment)
		// {
		// 	// NOTE: https://stackoverflow.com/q/26320673 suggests that this approach won't work: when there's no element match, it won't know where to insert the item.
		// 	// So I'm going to have to come up with a different approach.
		// 	foreach (LfCommentReply reply in comment.Replies)
		// 	{
		// 		if (reply.Guid == null) continue;
		// 		var filter = Builders<LfComment>.Filter.ElemMatch(cmt => cmt.Replies, r => r.Guid == reply.Guid);
		// 		DateTime utcNow = DateTime.UtcNow;
		// 		var update = Builders<LfComment>.Update.SetOnInsert(cmt => cmt.Replies[-1].UniqId, PseudoPhp.NonUniqueIdFromDateTime(utcNow));
		// 		update.SetOnInsert(cmt => cmt.Replies[-1].Guid, reply.Guid);
		// 		update.SetOnInsert(cmt => cmt.Replies[-1].AuthorInfo.CreatedDate, reply.AuthorInfo.CreatedDate);
		// 		// We only set the modified date on insert, so that if LF changes it, we won't overwrite that.
		// 		update.SetOnInsert(cmt => cmt.Replies[-1].AuthorInfo.ModifiedDate, reply.AuthorInfo.ModifiedDate);
		// 		update.SetOnInsert(cmt => cmt.Replies[-1].Content, reply.Content);  // TODO: What happens if someone edits it in LF? Are we going to overwrite it? If so, then Set rather than SetOnInsert.
		// 		update.Set(cmt => cmt.Replies[-1].AuthorNameAlternate, reply.AuthorNameAlternate);
		// 		update.Set(cmt => cmt.Replies[-1].IsDeleted, reply.IsDeleted);
		// 		yield return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = true };
		// 	}
		// }

		public UpdateOneModel<LfComment> PrepareUpdateCommentReplyGuidForUniqId(string uniqid, Guid guid)
		{
			// The "-1" index, according to https://stackoverflow.com/q/42396877/, is the positional index "$" in Mongo.
			// I *really* wish this had been documented in the MongoDB documentation. ANYWHERE AT ALL. *Sigh*...
			var filter = Builders<LfComment>.Filter.ElemMatch(comment => comment.Replies, r => r.UniqId == uniqid);
			var update = Builders<LfComment>.Update.Set(comment => comment.Replies[-1].Guid, guid);
			return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = false };
		}

		public UpdateOneModel<LfComment> PrepareUpdateCommentGuidForObjectId(ObjectId objectId, Guid guid)
		{
			var filter = Builders<LfComment>.Filter.Eq(comment => comment.Id, objectId);
			var update = Builders<LfComment>.Update.Set(comment => comment.Guid, guid);
			return new UpdateOneModel<LfComment>(filter, update) { IsUpsert = false };
		}

		// public UpdateOneModel<BsonDocument> OldVersionOfPrepareUpdateCommentReplyGuidForUniqId(string uniqid, string guid)
		// {
		// 	// I'd like to write this in the C# type-safe format, but http://stackoverflow.com/q/28945108/ suggests that that's not possible
		// 	// But WAIT! https://stackoverflow.com/q/42396877/ says to use the "special" index -1, e.g. replies[-1].guid. That's... wow, WHERE is that documented?
		// 	var filter = new BsonDocument("replies.id", uniqid);  // The field is called UniqId in C#, but just "id" in Mongo/BSON
		// 	var update = new BsonDocument("$set", new BsonDocument("replies.$.guid", guid));
		// 	return new UpdateOneModel<BsonDocument>(filter, update) { IsUpsert = false };
		// }

		public void SetCommentReplyGuids(ILfProject project, IDictionary<string,Guid> uniqIdToGuidMappings)
		{
			if (uniqIdToGuidMappings == null || uniqIdToGuidMappings.Count <= 0)
			{
				// Nothing to do! And BulkWrite *requires* at least one update, otherwise Mongo will throw an
				// error. So it would cause an error to proceed if there are no uniqid -> GUID mappings to write.
				return;
			}
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfComment> collection = db.GetCollection<LfComment>(MagicStrings.LfCollectionNameForLexiconComments);
			var updates = new List<UpdateOneModel<LfComment>>(uniqIdToGuidMappings.Count);
			foreach (KeyValuePair<string, Guid> kv in uniqIdToGuidMappings)
			{
				string uniqid = kv.Key;
				Guid guid = kv.Value;
				UpdateOneModel<LfComment> update = PrepareUpdateCommentReplyGuidForUniqId(uniqid, guid);
				updates.Add(update);
			}
			var options = new BulkWriteOptions { IsOrdered = false };
			var result = collection.BulkWrite(updates, options);
		}

		public void SetCommentGuids(ILfProject project, IDictionary<string,Guid> commentIdToGuidMappings)
		{
			if (commentIdToGuidMappings == null || commentIdToGuidMappings.Count <= 0)
			{
				// Nothing to do! And BulkWrite *requires* at least one update, otherwise Mongo will throw an
				// error. So it would cause an error to proceed if there are no uniqid -> GUID mappings to write.
				return;
			}
			IMongoDatabase db = GetProjectDatabase(project);
			IMongoCollection<LfComment> collection = db.GetCollection<LfComment>(MagicStrings.LfCollectionNameForLexiconComments);
			var updates = new List<UpdateOneModel<LfComment>>(commentIdToGuidMappings.Count);
			foreach (KeyValuePair<string, Guid> kv in commentIdToGuidMappings)
			{
				string objectIdStr = kv.Key;
				Guid guid = kv.Value;
				ObjectId objectId;
				if (String.IsNullOrEmpty(objectIdStr) || ! ObjectId.TryParse(objectIdStr, out objectId))
				{
					continue;
				}
				UpdateOneModel<LfComment> update = PrepareUpdateCommentGuidForObjectId(objectId, guid);
				updates.Add(update);
			}
			var options = new BulkWriteOptions { IsOrdered = false };
			var result = collection.BulkWrite(updates, options);
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
						var sensesFieldName = "senses.fields." + fieldName;
						if (!_vernacularWsFieldsList.Contains(sensesFieldName) && !_pronunciationWsFieldsList.Contains(sensesFieldName))
							analysisFields.Add(sensesFieldName);
					}
					if (senses.Fields.ContainsKey("examples"))
					{
						LfConfigFieldList examples = (LfConfigFieldList)senses.Fields["examples"];
						IEnumerable<string> exampleFields = examples.Fields.Where(kv => kv.Value is LfConfigMultiText).Select(kv => kv.Key);
						foreach (string fieldName in exampleFields)
						{
							var examplesFieldName = "senses.fields.examples.fields." + fieldName;
							if (!_vernacularWsFieldsList.Contains(examplesFieldName) && !_pronunciationWsFieldsList.Contains(examplesFieldName))
								analysisFields.Add(examplesFieldName);
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

		public BsonDocument GetRoleViews(ILfProject project)
		{
			MongoProjectRecord projectRecord = GetProjectRecord(project);
			if (projectRecord == null)
				return new BsonDocument();
			return projectRecord.Config.RoleViews;
		}

		public BsonDocument GetUserViews(ILfProject project)
		{
			MongoProjectRecord projectRecord = GetProjectRecord(project);
			if (projectRecord == null)
				return new BsonDocument();
			return projectRecord.Config.UserViews;
		}

		/// <summary>
		/// Remove previous project custom field configurations that no longer exist,
		/// and then update them at the appropriate entry, senses, and examples level.
		/// Also adds these field names to the displayed fieldOrder.
		/// </summary>
		/// <param name="project">LF project</param>
		/// <param name="lfCustomFieldList"> Dictionary of LF custom field settings</param>
		/// <returns>True if mongodb was updated</returns>
		public bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList, Dictionary<string, string> lfCustomFieldTypes)
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

			Dictionary<string, LfConfigFieldBase> customFieldConfig = GetCustomFieldConfig(project);
			BsonDocument roleViews = GetRoleViews(project);
			BsonDocument userViews = GetUserViews(project);

			List<string> roleViewNames = roleViews != null ? roleViews.Names.ToList<string>() : new List<string>();
			List<string> userViewNames = userViews != null ? userViews.Names.ToList<string>() : new List<string>();
			// Note that userViewNames doesn't contain usernames like "rmunn", but ObjectId strings like "54c780ea863f1c2127635ca9"

			// Clean out previous fields and fieldOrders that no longer exist (removed from FDO)
			foreach (string customFieldNameToRemove in customFieldConfig.Keys.Except(lfCustomFieldList.Keys.ToList()))
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
				// User views and role views are stored "flat", not nested under senses/examples
				foreach (string viewName in roleViewNames) {
					previousUpdates.Add(builder.Unset(String.Format("config.roleViews.{0}.fields.{1}", viewName, customFieldNameToRemove)));
				}
				foreach (string viewName in userViewNames) {
					previousUpdates.Add(builder.Unset(String.Format("config.userViews.{0}.fields.{1}", viewName, customFieldNameToRemove)));
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
				string fieldName = customFieldKVP.Key;
				LfConfigFieldBase fieldConfig = customFieldKVP.Value;

				string fieldType;
				if (!lfCustomFieldTypes.TryGetValue(fieldName, out fieldType)) {
					fieldType = "basic";
				}
				BsonDocument viewFieldConfig = LexViewFieldConfigFactory.CreateBsonDocumentByType(fieldType);

				foreach (var viewName in roleViewNames) {
					BsonValue value = roleViews.GetValue(viewName);
					if (value != null && value.IsBsonDocument) {
						BsonDocument view = value.AsBsonDocument;
						if (!view.Contains(fieldName)) {
							currentUpdates.Add(builder.Set(String.Format("config.roleViews.{0}.fields.{1}", viewName, fieldName), viewFieldConfig));
						}
					}
				}

				foreach (var viewName in userViewNames) {
					BsonValue value = userViews.GetValue(viewName);
					if (value != null && value.IsBsonDocument) {
						BsonDocument view = value.AsBsonDocument;
						if (!view.Contains(fieldName)) {
							currentUpdates.Add(builder.Set(String.Format("config.userViews.{0}.fields.{1}", viewName, fieldName), viewFieldConfig));
						}
					}
				}

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

		private static UpdateDefinition<TDocument> BuildUpdate<TDocument>(TDocument doc, bool ignoreDirty)
		{
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
				if (ignoreDirty && prop.Name == "DirtySR")
				{
					// We don't want to set DirtySR because we'll set it later. If we set it here
					// as well we get an exception with Mongo 3.6.
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
			UpdateDefinition<LfLexEntry> coreUpdate = BuildUpdate(data, true);
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
				return updateResult != null;
			}

			var updateBuilder = Builders<LfLexEntry>.Update;
			/* Commented out to check if DirtySR was the problem. - 2018-07 RM
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
			return updateResult != null;
			*/
			updateResult = coll.FindOneAndUpdate(filter, coreUpdate, upsert);
			return updateResult != null;
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
			UpdateDefinition<TDocument> update = BuildUpdate(data, false);
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

		public bool SetLastSyncedDate(ILfProject project, DateTime? newSyncedDate)
		{
			// Rather than use UpdateRecordImpl, we want to build a custom update that updates just this one record.
			var filterBuilder = Builders<MongoProjectRecord>.Filter;
			var updateBuilder = Builders<MongoProjectRecord>.Update;
			var filter = filterBuilder.Eq(record => record.ProjectCode, project.ProjectCode);
			var update = updateBuilder.Set(record => record.LastSyncedDate, newSyncedDate);
			var mainDb = GetMainDatabase();
			var collection = mainDb.GetCollection<MongoProjectRecord>(MagicStrings.LfCollectionNameForProjectRecords);
			var updateOptions = new FindOneAndUpdateOptions<MongoProjectRecord> {
				IsUpsert = false  // I believe this is the default, but safer to be explicit
			};
			var result = collection.FindOneAndUpdate(filter, update, updateOptions);
			return (result != null);
		}
	}
}

