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
			var client = new MongoClient(HardcodedMongoConnectionString);
			// TODO: Just for testing, we'll get a list of database names and collections in each one
			IEnumerable<string> dbTask = GetListOfMongoDatabases(client);
			Console.WriteLine(dbTask.GetType());
			foreach (string dbName in dbTask)
			{
				Console.WriteLine("Database named {0}", dbName);
				IMongoDatabase db = client.GetDatabase(dbName);
				foreach (string collName in GetListOfCollections(db))
				{
					Console.WriteLine("    has collection {0}", collName);
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

