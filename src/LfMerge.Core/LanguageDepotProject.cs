// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;

namespace LfMerge.Core
{
	public class LanguageDepotProject: ILanguageDepotProject
	{
		private LfMergeSettings Settings { get; set; }
		private ILogger Logger { get; set; }

		// TODO: Need to grab a MongoConnection as well
		public LanguageDepotProject(LfMergeSettings settings, ILogger logger)
		{
			Logger = logger;
			Settings = settings;
		}

		public void Initialize(string lfProjectCode)
		{
			// TODO: This should use the MongoConnection class instead
			MongoClient client = new MongoClient("mongodb://" + Settings.MongoDbHostNameAndPort);
			IMongoDatabase database = client.GetDatabase("scriptureforge");
			IMongoCollection<BsonDocument> projectCollection = database.GetCollection<BsonDocument>("projects");
			//var userCollection = database.GetCollection<BsonDocument>("users");

			var projectFilter = new BsonDocument("projectCode", lfProjectCode);
			var list = projectCollection.Find(projectFilter).ToList();

			var project = list.FirstOrDefault();
			if (project == null)
				throw new ArgumentException("Can't find project code", "lfProjectCode");

			BsonValue value, srProjectValue;
			if (project.TryGetValue("sendReceiveProjectIdentifier", out value))
			{
				if (value == null || value.BsonType == BsonType.Null)
				{
					Logger.Error("sendReceiveProjectIdentifier was null for LF project code {0}", lfProjectCode);
					throw new ArgumentNullException("sendReceiveProjectIdentifier"); // TODO: Can we set a default value?
				}
				Identifier = value.AsString;
			}
			if (project.TryGetValue("sendReceiveProject", out srProjectValue))
			{
				if (srProjectValue == null || srProjectValue.BsonType == BsonType.Null)
				{
					Logger.Error("sendReceiveProject was null for LF project code {0}", lfProjectCode);
					throw new ArgumentNullException("sendReceiveProject"); // TODO: Can we set a default value?
				}
				if (srProjectValue.BsonType != BsonType.Document)
				{
					Logger.Error("sendReceiveProject should be a BsonDocument for LF project code {0}; instead, found a {1}", lfProjectCode, srProjectValue.BsonType.ToString());
					throw new InvalidCastException(); // TODO: Can we set a default value?
				}
				if (srProjectValue.AsBsonDocument.TryGetValue("repository", out value))
				{
					if (value == null || value.BsonType == BsonType.Null)
					{
						Logger.Error("repository was null for LF project code {0}", lfProjectCode);
						throw new ArgumentNullException("repository"); // TODO: Can we set a default value?
					}
					Repository = value.AsString;
				}
			}
		}

		public string Identifier { get; private set; }

		public string Repository { get; private set; }
	}
}

