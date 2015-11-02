// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LfMerge.Actions
{
	public class UpdateFdoFromMongoDbAction: Action
	{
		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.QUEUED; }
		}

		protected override void DoRun(ILfProject project)
		{
			// GetLexiconForTesting(project);
			GetConfigForTesting(project);
		}

		private void GetLexiconForTesting(ILfProject project)
		{
			var db = MongoConnection.Default.GetProjectDatabase(project);
			var collection = db.GetCollection<BsonDocument>("lexicon");

			// Can't use LINQ here as that requires Mongo server version 2.2 or higher

			List<BsonDocument> result = collection.Find<BsonDocument>(_ => true).ToListAsync().Result;
			foreach (BsonDocument item in result)
			{
				Console.WriteLine(item);
			}

			Console.WriteLine("Now trying an enumerable:");
			IAsyncCursor<BsonDocument> result2 = collection.Find<BsonDocument>(_ => true).ToCursorAsync().Result;
			foreach (BsonDocument item in result2.AsEnumerable())
			{
				Console.WriteLine(item);
			}
		}

		private void GetConfigForTesting(ILfProject project)
		{
			MongoProjectRecord projectRecord = MongoProjectRecord.Create(project);
			var config = projectRecord.Config;
			Console.WriteLine(config.GetType()); // Should be LfMerge.LanguageForge.Config.LfProjectConfig
			Console.WriteLine(config.Entry.Type);
			Console.WriteLine(String.Join(", ", config.Entry.FieldOrder));
			Console.WriteLine(config.Entry.Fields["lexeme"].Type);
			Console.WriteLine(config.Entry.Fields["lexeme"].GetType());
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

