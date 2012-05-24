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
		private LfDirectoriesAndFiles _lfDirectories;
		private String LangForgeVersion = "1.0";

		/// <summary>
		///
		/// </summary>
		/// <param name="languageForgeServerFolder">This must be the path to the Language Forge folder on the server.</param>
		public LiftUpdateProcessor(String languageForgeServerFolder)
		{
			_lfDirectories = new LfDirectoriesAndFiles(languageForgeServerFolder);
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
				}

				var repo = new HgRepository(projMergeFolder, new NullProgress());  //
				String currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
				var shas = _liftUpdateScanner.GetShasForAProjectName(project);
				//foreach sha, apply all the updates. We may need to change to that sha on the repo so check for this
				//before doing the lfSynchronicMerger
				foreach (String sha in shas)
				{
					if (!(currentSha.Equals(sha)))
					{
						//We are not at the right revision to apply the .lift.update files, so save any changes on the
						//current revision (this only commits if the .lift file has changed)
						repo.Commit(false, String.Format("Language Forge version {0} commit", LangForgeVersion));
						//next change to the correct sha so that the .lift.update fies are applied to the correct version
						repo.Update(sha);
						currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
					}
					var updatefilesForThisSha = _liftUpdateScanner.GetUpdateFilesArrayForProjectAndSha(project, sha);
					_lfSynchMerger.MergeUpdatesIntoFile(_lfDirectories.LiftFileMergePath(project), updatefilesForThisSha);
				}
			}
		}
	}
}
