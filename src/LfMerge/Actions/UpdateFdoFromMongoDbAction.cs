// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;

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
			ILfProjectConfig config = GetConfigForTesting(project);
			if (config == null)
				return;
			GetLexiconForTesting(project, config);
		}

		private void GetLexiconForTesting(ILfProject project, ILfProjectConfig config)
		{
			var db = MongoConnection.Default.GetProjectDatabase(project);
			var collection = db.GetCollection<LfLexEntry>("lexicon");

			// Can't use LINQ here as that requires Mongo server version 2.2 or higher

			List<LfLexEntry> result = collection.Find<LfLexEntry>(_ => true).ToListAsync().Result;
			foreach (LfLexEntry item in result)
			{
				Console.WriteLine(String.Join(", ", item.Lexeme.Values.Select(x => x.Value)));
			}
			// 56332f680f8709ed0fd92d6c is ObjectId for test data. TODO: Actually find by lexicon value

			Console.WriteLine("Now trying an enumerable:");
			IAsyncCursor<LfLexEntry> result2 = collection.Find<LfLexEntry>(_ => true).ToCursorAsync().Result;
			foreach (LfLexEntry item in result2.AsEnumerable())
			{
				Console.WriteLine(String.Join(", ", item.Lexeme.Values.Select(x => x.Value)));
			}
		}

		private ILfProjectConfig GetConfigForTesting(ILfProject project)
		{
			MongoProjectRecord projectRecord = MongoProjectRecord.Create(project);
			if (projectRecord == null)
			{
				Console.WriteLine("No project named {0}", project.LfProjectCode);
				Console.WriteLine("If we are unit testing, this may not be an error");
				return null;
			}
			ILfProjectConfig config = projectRecord.Config;
			Console.WriteLine(config.GetType()); // Should be LfMerge.LanguageForge.Config.LfProjectConfig
			Console.WriteLine(config.Entry.Type);
			Console.WriteLine(String.Join(", ", config.Entry.FieldOrder));
			Console.WriteLine(config.Entry.Fields["lexeme"].Type);
			Console.WriteLine(config.Entry.Fields["lexeme"].GetType());
			return config;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

