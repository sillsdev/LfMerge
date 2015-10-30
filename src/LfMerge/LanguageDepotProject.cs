// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace LfMerge
{
	public class LanguageDepotProject
	{
		public LanguageDepotProject(string lfProjectCode)
		{
			var client = new MongoClient("mongodb://" + LfMergeSettings.Current.MongoDbHostNameAndPort);
			var database = client.GetDatabase("languageforge");
			var collection = database.GetCollection<BsonDocument>("projects");

			var filter = new BsonDocument("projectCode", lfProjectCode);
			var list = collection.Find(filter).ToListAsync();
			list.Wait();

			var project = list.Result.FirstOrDefault();
			if (project == null)
				throw new ArgumentException("Can't find project code", "lfProjectCode");

			BsonValue value;
			if (project.TryGetValue("ldUsername", out value))
				Username = value.AsString;
			if (project.TryGetValue("ldPassword", out value))
				Password = value.AsString;
			if (project.TryGetValue("ldProjectCode", out value))
				ProjectCode = value.AsString;
		}

		public string Username { get; private set; }

		public string Password { get; private set; }

		public string ProjectCode { get; private set; }
	}
}

