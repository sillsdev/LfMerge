using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using System.Windows.Forms;
using Palaso.Progress.LogBox;

namespace lfmergelift
{
	class LiftUpdateProcessor
	{
		private HgRepository _hgRepo;
		private LiftUpdatesScanner _liftUpdateScanner;
		private LfSynchronicMerger _lfSynchMerger;
		private LangForgeDirectories _lfDirectories;

		/// <summary>
		///
		/// </summary>
		/// <param name="languageForgeServerFolder">This must be the path to the Language Forge folder on the server.</param>
		public LiftUpdateProcessor(String languageForgeServerFolder)
		{
			_lfDirectories = new LangForgeDirectories(languageForgeServerFolder);
			_liftUpdateScanner = new LiftUpdatesScanner(_lfDirectories.LiftUpdatesPath);
			_lfSynchMerger = new LfSynchronicMerger();
		}

		public void ProcessLiftUpdates()
		{
			//If there are .lift.update files to be processed then do nothing
			if (!_liftUpdateScanner.ScannerHasListOfLiftUpdates)
				return;

			//Get the projects which need to have .lift.updates applied to them
			var projects = _liftUpdateScanner.GetProjectsNamesToUpdate();
			foreach (var project in projects)
			{
				//first check if the project has been cloned to the mergeWork folder. If not then clone it.
				var projMergeFolder = _lfDirectories.GetProjMergePath(project);
				if (!Directory.Exists(projMergeFolder))
				{
					//Create the folder to clone the project into.
					_lfDirectories.CreateMergeWorkProjectFolder(project);
					Debug.Assert(Directory.Exists(projMergeFolder));
					//Get path for the repository then clone it to the webWork location
					var projWebFolder = _lfDirectories.GetProjWebPath(project);
					HgRepository.Clone(projWebFolder, projMergeFolder, new NullProgress());

					//Note: should there be checking done to verify that the .Lift file exists in the new location??
				}

				var shas = _liftUpdateScanner.GetShasForAProjectName(project);
				//foreach sha, apply all the updates. We may need to change to that sha on the repo so check for this
				//before doing the lfSynchronicMerger
				foreach (var sha in shas)
				{

				}
			}

		}

	}
}
