// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Driver;

namespace LfMerge.MongoConnector
{
	public class MongoProjectRecordFactory
	{
		public IMongoConnection Connection { get; private set; }

		public MongoProjectRecordFactory(IMongoConnection connection)
		{
			Connection = connection;
		}

		public virtual MongoProjectRecord Create(ILfProject project)
		{
			if (project == null)
				return null;
			if (Connection == null)
				return null;

			IMongoDatabase db = Connection.GetMainDatabase();
			IMongoCollection<MongoProjectRecord> coll = db.GetCollection<MongoProjectRecord>(MongoProjectRecord.ProjectsCollectionName);
			MongoProjectRecord record =
				coll.Find(proj => proj.ProjectCode == project.LfProjectCode)
					.Limit(1).FirstOrDefaultAsync().Result;
			return record;
		}

	}
}

