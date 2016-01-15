// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Driver;

namespace LfMerge.MongoConnector
{
	public interface IMongoConnection
	{
		IMongoDatabase GetDatabase(string databaseName);
		IMongoDatabase GetProjectDatabase(ILfProject project);
		IMongoDatabase GetMainDatabase(); // TODO: Maybe remove this one?
		UpdateDefinition<TDocument> BuildUpdate<TDocument>(TDocument doc);
	}
}

