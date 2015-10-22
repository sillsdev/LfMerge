// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using LfMerge.Queues;
using LfMerge.FieldWorks;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Driver.Core;
using MongoDB.Bson;

namespace LfMerge
{
	class MainClass
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Console.WriteLine("Starting");
			Task dbTask = GetListOfMongoDatabases(); // TODO: Just for testing, for now.
			Console.WriteLine(dbTask.GetType());
			dbTask.Wait(); // Don't forget to wait, or the asynchronous Mongo calls won't have time to run
			Console.WriteLine("Stopping");
			return;
			/*
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			// TODO: read settings from config instead of hard coding them here
			var baseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			LfMergeSettings.Initialize(baseDir);

			for (var queue = Queue.FirstQueueWithWork;
				queue != null;
				queue = queue.NextQueueWithWork)
			{
				foreach (var projectName in queue.QueuedProjects)
				{
					var project = LanguageForgeProject.Create(projectName);

					for (var action = queue.CurrentAction;
						action != null;
						action = action.NextAction)
					{
						action.Run(project);
					}
				}
			}

			var database = args.Length > 1 ? args[0] : "Sena 3";

			using (var fw = new FwProject(database))
			{
				// just some test output
				var fdoCache = fw.Cache;
				Console.WriteLine("Ethnologue Code: {0}", fdoCache.LangProject.EthnologueCode);
				Console.WriteLine("Interlinear texts:");
				foreach (var t in fdoCache.LangProject.InterlinearTexts)
				{
					Console.WriteLine("{0:D6}: title: {1} (comment: {2})", t.Hvo,
						t.Title.BestVernacularAlternative.Text,
						t.Comment.BestVernacularAnalysisAlternative.Text);
				}
			}*/
		}

		public async static Task<bool> GetListOfMongoDatabases()
		{
			// TODO: Get connection string from config, not hardcoded
			string HardcodedMongoConnectionString = "mongodb://languageforge.local/scriptureforge";
			var client = new MongoClient(HardcodedMongoConnectionString);
			IAsyncCursor<BsonDocument> dbs = await client.ListDatabasesAsync();
			// TODO: Figure out if the second "await" is truly necessary.
			// Perhaps we can chain this together with Task.ContinueWith or something.
			await dbs.ForEachAsync(ProcessOneDbDocument);
			return true;
		}

		public static void ProcessOneDbDocument(BsonDocument doc) {
			var d = doc.ToDictionary();
			foreach (var kv in d)
			{
				// Console.WriteLine("{0}: {1}", kv.Key, kv.Value);
			}
			Console.WriteLine("Database name: {0}", doc.GetElement("name").Value);
		}

	}
}
