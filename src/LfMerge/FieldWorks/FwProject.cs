// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using System.Threading.Tasks;
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
			Task dbTask = GetListOfMongoDatabases(); // TODO: Just for testing, for now.
			Console.WriteLine(dbTask.GetType());
			dbTask.Wait(); // Don't forget to wait, or the asynchronous Mongo calls won't have time to run
			Console.WriteLine("Stopping UpdateFdoFromMongoDb");
			return;

		private async static Task<bool> GetListOfMongoDatabases()
		{
			// TODO: Get connection string from config, not hardcoded
			string HardcodedMongoConnectionString = "mongodb://languageforge.local/scriptureforge";
			var client = new MongoClient(HardcodedMongoConnectionString);
			await client.ListDatabasesAsync().ContinueWith(task =>
				task.Result.ForEachAsync(doc => ProcessOneDbDocument(doc)));
			return true;
		}

		private static void ProcessOneDbDocument(BsonDocument doc) {
			var d = doc.ToDictionary();
			foreach (var kv in d)
			{
				// Console.WriteLine("{0}: {1}", kv.Key, kv.Value);
			}
			Console.WriteLine("Database name: {0}", doc.GetElement("name").Value);
		}

	}
}

