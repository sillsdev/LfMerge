// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.Settings;

namespace LfMerge
{
	public class LanguageDepotProject: ILanguageDepotProject
	{
		private LfMergeSettingsIni Settings { get; set; }

		// TODO: Need to grab a MongoConnection as well
		public LanguageDepotProject(LfMergeSettingsIni settings)
		{
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
			if (project.TryGetValue("sendReceiveProject", out srProjectValue) &&
			   (srProjectValue.AsBsonDocument.TryGetValue("identifier", out value)))
				Identifier = value.AsString;
			if (project.TryGetValue("sendReceiveProject", out srProjectValue) &&
				(srProjectValue.AsBsonDocument.TryGetValue("repository", out value)))
				Repository = value.AsString;
			if (project.TryGetValue("sendReceiveUsername", out value))
				Username = value.AsString;
			if (project.TryGetValue("sendReceivePassword", out value))
				Password = value.AsString;
		}

		public string Username { get; private set; }

		public string Password { get; private set; }

		public string Identifier { get; private set; }

		public string Repository { get; private set; }
	}
}

