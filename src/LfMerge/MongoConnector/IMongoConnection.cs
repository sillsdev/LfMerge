// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using MongoDB.Driver;
using LfMerge.LanguageForge.Config;

namespace LfMerge.MongoConnector
{
	public interface IMongoConnection
	{
		IMongoDatabase GetProjectDatabase(ILfProject project);
		IMongoDatabase GetMainDatabase(); // TODO: Maybe remove this one?
		IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName);
		bool UpdateRecord<TDocument>(ILfProject project, TDocument data, Guid guid, string collectionName);
	}
}

