// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.LanguageForge.Model;

namespace LfMerge.MongoConnector
{
	public interface IMongoConnection
	{
		IMongoDatabase GetProjectDatabase(ILfProject project);
		IMongoDatabase GetMainDatabase(); // TODO: Maybe remove this one?
		IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName);
		bool UpdateRecord<TDocument>(ILfProject project, TDocument data, Guid guid, string collectionName, MongoDbSelector whichDb = MongoDbSelector.ProjectDatabase);
		bool UpdateRecord<TDocument>(ILfProject project, TDocument data, ObjectId id, string collectionName, MongoDbSelector whichDb = MongoDbSelector.ProjectDatabase);
		Dictionary<string, LfInputSystemRecord>GetInputSystems(ILfProject project);
		bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems);
	}
}

