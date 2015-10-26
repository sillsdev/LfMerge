// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using System.Linq;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SIL.Utils;
using MongoDB.Driver;
using MongoDB.Driver.Core;
using MongoDB.Bson;

namespace LfMerge.FieldWorks
{
	/// <summary>
	/// A FieldWorks project
	/// </summary>
	public class FwProject: IDisposable
	{
		private readonly IThreadedProgress _progress = new ThreadedProgress();
		private readonly IFdoUI _fdoUi;
		private readonly ProjectIdentifier _project;

		public FwProject(string database)
		{
			_project = new ProjectIdentifier(LfMergeSettings.Current, database);
			_fdoUi = new ConsoleFdoUi(_progress.SynchronizeInvoke);
			Cache = TryGetFdoCache();
		}

		#region Disposable stuff

		#if DEBUG
		/// <summary/>
		~FwProject()
		{
			Dispose(false);
		}
		#endif

		/// <summary/>
		public bool IsDisposed { get; private set; }

		/// <summary/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary/>
		protected virtual void Dispose(bool fDisposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!fDisposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			if (fDisposing && !IsDisposed)
			{
				// dispose managed and unmanaged objects
				if (Cache != null)
					Cache.Dispose();
			}
			Cache = null;
			IsDisposed = true;
		}

		#endregion

		public FdoCache Cache { get; private set; }

		private FdoCache TryGetFdoCache()
		{
			FdoCache fdoCache = null;
			var path = _project.Path;
			if (!File.Exists(path))
			{
				return null;
			}

			var settings = new FdoSettings {DisableDataMigration = true};

			try
			{
				fdoCache = FdoCache.CreateCacheFromExistingData(
					_project, Thread.CurrentThread.CurrentUICulture.Name, _fdoUi,
					_project.FdoDirectories, settings, _progress);
			}
			catch (FdoDataMigrationForbiddenException)
			{
				Console.WriteLine("Error: Incompatible version");
				return null;
			}
			catch (FdoNewerVersionException)
			{
				Console.WriteLine("Error: Incompatible version");
				return null;
			}
			catch (FdoFileLockedException)
			{
				Console.WriteLine("Error: Access denied");
				return null;
			}
			catch (StartupException)
			{
				Console.WriteLine("Error: Unknown error");
				return null;
			}

			return fdoCache;
		}
			Console.WriteLine("Starting UpdateFdoFromMongoDb");
			// TODO: Get connection string from config, not hardcoded
			string HardcodedMongoConnectionString = "mongodb://languageforge.local/scriptureforge";
			string HardcodedProjectCode = "thai_food";
			string HardcodedDatabasePrefix = "sf_"; // TODO: Is there a better way to get the prefix?
			//string DatabaseName = "sf_" + HardcodedProjectCode;
			var client = new MongoClient(HardcodedMongoConnectionString);
			var sf = client.GetDatabase("scriptureforge");
			var projects = sf.GetCollection<BsonDocument>("projects");
			var F = Builders<BsonDocument>.Filter; // Make the next line shorter to read
			var filter = F.Eq("appName", "lexicon") & F.Eq("projectCode", HardcodedProjectCode);
			var project = projects.Find(filter).Limit(1).FirstOrDefaultAsync().Result; // Filter on server
			//var project = projects.Find(doc => doc["appName"] == "lexicon" && doc["projectCode"] == HardcodedProjectCode)
			//	.Limit(1).ToListAsync().Result.FirstOrDefault(); // Filter on client
			if (project != null)
			{
				Console.WriteLine("Project \"{0}\" has code {1}", project["projectName"], project["projectCode"]);
				var config = project["config"];
				var fields = config["entry"]["fieldOrder"] as BsonArray;
				foreach (var field in fields)
					Console.WriteLine("  and has field: {0}", field);
				string dbName = HardcodedDatabasePrefix + project["projectCode"];
				var db = client.GetDatabase(dbName);
				var collection = db.GetCollection<BsonDocument>("lexicon");
				var bar = collection.Find(_ => true).ToListAsync().Result; // Without the Result, we would have to use await
				foreach (var item in bar)
				{
					// To do this properly, we should get the inputSystems from the config["entry"]["fields"]["lexeme"] list
					// For now, we hardcode the two th-fonipa and th writing system names
					string th_fonipa;
					try {
						th_fonipa = item["lexeme"]["th-fonipa"]["value"].AsString;
						Console.WriteLine("Item: {0}", th_fonipa);
					}
					catch(KeyNotFoundException) {
						Console.WriteLine("No fonipa for {0}", item["lexeme"]["th"]["value"]);
					}
				}

			}
			Console.WriteLine("Stopping UpdateFdoFromMongoDb");

			return;

		private static IEnumerable<string> GetListOfMongoDatabases(MongoClient client)
		{
			IAsyncCursor<BsonDocument> foo = client.ListDatabasesAsync().Result;
			List<BsonDocument> l = foo.ToListAsync().Result;
			IEnumerable<string> result = l.Select(doc => doc["name"].AsString);
			return result;
		}

		private static IEnumerable<string> GetListOfCollections(IMongoDatabase db)
		{
			IAsyncCursor<BsonDocument> foo = db.ListCollectionsAsync().Result;
			List<BsonDocument> l = foo.ToListAsync().Result;
			IEnumerable<string> result = l.Select(doc => doc["name"].AsString);
			return result;
		}

	}
}

