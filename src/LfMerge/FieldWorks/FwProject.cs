// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.Utils;

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

		public FwProject(ILfMergeSettings settings, string database)
		{
			_project = new ProjectIdentifier(settings, database);
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
				Console.WriteLine("Error: Incompatible version (can't migrate data)");
				return null;
			}
			catch (FdoNewerVersionException)
			{
				Console.WriteLine("Error: Incompatible version (version number newer than expected)");
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
	}
}

