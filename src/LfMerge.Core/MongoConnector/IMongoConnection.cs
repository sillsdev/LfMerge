// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using MongoDB.Bson;
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
		IEnumerable<LfLexEntry> GetLfLexEntryGuids(ILfProject project, bool includeDeletedEntries = false);
		bool MarkLfLexEntryDeleted(ILfProject project, Guid guid);
		Dictionary<string, LfInputSystemRecord>GetInputSystems(ILfProject project);
		bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			List<string> vernacularWss, List<string> analysisWss, List<string> pronunciationWss);
		bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList);
		Dictionary<string, LfConfigFieldBase> GetCustomFieldConfig(ILfProject project);
		bool SetLastSyncedDate(ILfProject project, DateTime? newSyncedDate);  // TODO: Decide if this is really where this method belongs

		bool FlushBulkUpdates(); // NOTE: I don't want to actually use this API for real, this is just for testing purposes.
	}
}

