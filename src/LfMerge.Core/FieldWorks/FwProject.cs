// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using System.Xml;
using LfMerge.Core.Settings;
using SIL.LCModel;
using SIL.LCModel.Utils;

namespace LfMerge.Core.FieldWorks
{
	/// <summary>
	/// A FieldWorks project
	/// </summary>
	public class FwProject: IDisposable
	{
		private readonly IThreadedProgress _progress = new ThreadedProgress();
		private readonly ILcmUI _lcmUi;
		private readonly ProjectIdentifier _project;

		public FwProject(LfMergeSettings settings, string database)
		{
			_project = new ProjectIdentifier(settings.LcmDirectorySettings, database);
			_lcmUi = new ConsoleLcmUi(_progress.SynchronizeInvoke);
			Cache = TryGetLcmCache();
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

		public LcmCache Cache { get; private set; }

		public FwServiceLocatorCache ServiceLocator { get; private set; }

		private LcmCache TryGetLcmCache()
		{
			LcmCache lcmCache = null;
			var path = _project.Path;
			if (!File.Exists(path))
			{
				return null;
			}

			var settings = new LcmSettings {DisableDataMigration = !AllowDataMigration};

			try
			{
				lcmCache = LcmCache.CreateCacheFromExistingData(
					_project, Thread.CurrentThread.CurrentUICulture.Name, _lcmUi,
					_project.LcmDirectories, settings, _progress);
			}
			catch (LcmDataMigrationForbiddenException)
			{
				MainClass.Logger.Error("LCM: Incompatible version (can't migrate data)");
				return null;
			}
			catch (LcmNewerVersionException)
			{
				MainClass.Logger.Error("LCM: Incompatible version (version number newer than expected)");
				return null;
			}
			catch (LcmFileLockedException)
			{
				MainClass.Logger.Error("LCM: Access denied");
				return null;
			}
			catch (LcmInitializationException e)
			{
				MainClass.Logger.Error("LCM: Unknown error: {0}", e.Message);
				return null;
			}

			return lcmCache;
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

