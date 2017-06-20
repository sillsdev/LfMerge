// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using MongoDB.Driver;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;

namespace LfMerge.Core.MongoConnector
{
	public interface IMongoConnection
	{
		IMongoDatabase GetProjectDatabase(ILfProject project);
		IMongoDatabase GetMainDatabase();
		IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName);
		LfOptionList GetLfOptionListByCode(ILfProject project, string listCode);
		Dictionary<Guid, DateTime> GetAllModifiedDatesForEntries(ILfProject project);
		bool UpdateRecord(ILfProject project, LfLexEntry data);
		bool UpdateRecord(ILfProject project, LfOptionList data, string listCode);
		bool RemoveRecord(ILfProject project, Guid guid);
		Dictionary<string, LfInputSystemRecord>GetInputSystems(ILfProject project);
		IEnumerable<LfComment> GetComments(ILfProject project);
		void UpdateComments(ILfProject project, List<LfComment> comments);  // Should be void, but string for now as a very quick-and-dirty test
		void UpdateReplies(ILfProject project, List<Tuple<string, List<LfCommentReply>>> repliesFromFWWithCommentGuids);
		bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			List<string> vernacularWss, List<string> analysisWss, List<string> pronunciationWss);
		bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList);
		Dictionary<string, LfConfigFieldBase> GetCustomFieldConfig(ILfProject project);
		bool SetLastSyncedDate(ILfProject project, DateTime? newSyncedDate);  // TODO: Decide if this is really where this method belongs
		void SetCommentReplyGuids(ILfProject project, IDictionary<string,Guid> uniqIdToGuidMappings); // TODO: Implement in TestDouble
		Dictionary <MongoDB.Bson.ObjectId, Guid> GetGuidsByObjectIdForCollection(ILfProject project, string collectionName);

	}
}

