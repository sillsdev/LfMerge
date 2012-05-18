using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using Palaso.Lift.Merging;

namespace lfmergelift
{
	/// <summary>
	/// Changes made by Language Forge users will cause .lift.update files to be stored in the /mergeWork/updates folder on the server.
	/// This class provides functions for .lift.update files.
	///
	/// These files will have the following format:    ProjX_sha_timeStamp.lift.update
	/// </summary>
	internal class LiftUpdatesScanner
	{
		private HgRepository _hgRepo;
		private IEnumerable<UpdateInfo> _updateFilesInfo;
		private String _updatesDirectory;
		private FileInfo[] _liftUpdateFiles;

		public LiftUpdatesScanner(String updatesDirectory)
		{
			_updatesDirectory = updatesDirectory;
			_liftUpdateFiles = GetPendingUpdateFiles(_updatesDirectory);
		}

		public FileInfo[] LiftUpdateFiles
		{
			get { return _liftUpdateFiles; }
		}

		public static FileInfo[] GetPendingUpdateFiles(string pathToLiftUpdates)
		{
			//see ws-1035
			if (!pathToLiftUpdates.Contains(Path.DirectorySeparatorChar.ToString()))
			{
				throw new ArgumentException("pathToLiftUpdates must be a full path. Path was " + pathToLiftUpdates);
			}
			// ReSharper disable AssignNullToNotNullAttribute
			var di = new DirectoryInfo(pathToLiftUpdates);
			// ReSharper restore AssignNullToNotNullAttribute
			return di.GetFiles("*" + SynchronicMerger.ExtensionOfIncrementalFiles, SearchOption.TopDirectoryOnly);
		}
	}

	class UpdateInfo
	{
		string Project { get; set; }
		string Sha { get; set; }
		string UpdateFilePath { get; set; }
	}
}
