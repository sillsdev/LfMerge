// Copyright (c) 2016 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using System.Xml;
using LfMerge.Core.Settings;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.Utils;

namespace LfMerge.Core.FieldWorks
{
	/// <summary>
	/// A FieldWorks project
	/// </summary>
	public class FwProject: IDisposable
	{
		private readonly IThreadedProgress _progress = new ThreadedProgress();
		private readonly IFdoUI _fdoUi;
		private readonly ProjectIdentifier _project;

		public FwProject(LfMergeSettings settings, string database)
		{
			_project = new ProjectIdentifier(settings.FdoDirectorySettings, database);
			_fdoUi = new ConsoleFdoUi(_progress.SynchronizeInvoke);
			Cache = TryGetFdoCache();
			if (Cache != null)
			{
				ServiceLocator = new FwServiceLocatorCache(Cache.ServiceLocator);
			}
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

				ServiceLocator = null;

				// Dispose cache last
				if (Cache != null)
					Cache.Dispose();
				Cache = null;
			}
			IsDisposed = true;
		}

		#endregion

		public static bool AllowDataMigration { get; set; }

		public FdoCache Cache { get; private set; }

		public FwServiceLocatorCache ServiceLocator { get; private set; }

		private FdoCache TryGetFdoCache()
		{
			FdoCache fdoCache = null;
			var path = _project.Path;
			if (!File.Exists(path))
			{
				return null;
			}

			var settings = new FdoSettings {DisableDataMigration = !AllowDataMigration};

			try
			{
				fdoCache = FdoCache.CreateCacheFromExistingData(
					_project, Thread.CurrentThread.CurrentUICulture.Name, _fdoUi,
					_project.FdoDirectories, settings, _progress);
			}
			catch (FdoDataMigrationForbiddenException)
			{
				MainClass.Logger.Error("FDO: Incompatible version (can't migrate data)");
				return null;
			}
			catch (FdoNewerVersionException)
			{
				MainClass.Logger.Error("FDO: Incompatible version (version number newer than expected)");
				return null;
			}
			catch (FdoFileLockedException)
			{
				MainClass.Logger.Error("FDO: Access denied");
				return null;
			}
			catch (StartupException)
			{
				MainClass.Logger.Error("FDO: Unknown error");
				return null;
			}

			return fdoCache;
		}

		public static string GetModelVersion(string project)
		{
			if (!File.Exists(project))
				return null;

			using (var reader = new XmlTextReader(project))
			{
				if (!reader.ReadToFollowing("languageproject"))
				{
					MainClass.Logger.Error("Can't find <languageproject> element for '{0}'", project);
					return null;
				}
				return reader.GetAttribute("version");
			}
		}


	}
}

