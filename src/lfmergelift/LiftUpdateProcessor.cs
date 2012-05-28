using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using System.Windows.Forms;
using Chorus.sync;
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
				ProcessLiftUpdatesForProject(project);
			}
		}

		//make internal for tests.
		internal void ProcessLiftUpdatesForProject(string project)
		{
			var projMergeFolder = _lfDirectories.GetProjMergePath(project);
			//first check if the project has been cloned to the mergeWork folder. If not then clone it.
			if (!Directory.Exists(projMergeFolder))
			{
				CloneProjectToMergerWorkFolder(project, projMergeFolder);
			}

			var repo = new HgRepository(projMergeFolder, new NullProgress()); //
			var shas = _liftUpdateScanner.GetShasForAProjectName(project);
			//foreach sha, apply all the updates. We may need to change to that sha on the repo so check for this
			//before doing the lfSynchronicMerger
			foreach (String sha in shas)
			{
				ProcessUpdatesForAParticularSha(project, repo, sha);
			}
		}

		//make internal for tests.
		internal void ProcessUpdatesForAParticularSha(string project, HgRepository repo, string shaOfUpdateFiles)
		{
			String currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
			var allShas = repo.GetAllRevisions();
			if (!(currentSha.Equals(shaOfUpdateFiles)))
			{
				//We are not at the right revision to apply the .lift.update files, so save any changes on the
				//current revision (this only commits if the .lift file has changed)
				repo.Commit(false, String.Format("Language Forge version {0} commit", LangForgeVersion));
				allShas = repo.GetAllRevisions();
				var shaAfterCommit = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
				//next change to the correct sha so that the .lift.update fies are applied to the correct version
				var heads = repo.GetHeads();
				//This will give us the heads that now exist and if there are more than 1 (should not be more than 2) we need to merge them.
				if (heads.Count > 1)
				{
					//Some lift.update were applied to an older revision(sha) so the commit that was just done resulted in another head.
					//Therefore, merge in this head with the other one before applying any .lift.updates the other head.

					//repo.Merge(repo.PathToRepo, shaOfUpdateFiles);
					//repo.Merge(projMergeFolder, sha);


					Synchronizer synch = new Synchronizer(repo.PathToRepo,
														  new ProjectFolderConfiguration(repo.PathToRepo),
														  new NullProgress());
					SyncOptions options = new SyncOptions();
					options.DoPullFromOthers = false;
					options.DoMergeWithOthers = true;
					options.CheckinDescription = "Merge by Language Forge LiftUpdateProcessor";
					options.DoSendToOthers = false;

					SyncResults syncResults = synch.SyncNow(options);
					if (!syncResults.Succeeded)
						MessageBox.Show(syncResults.ErrorEncountered.Message, "synch.SyncNow failed");
					allShas = repo.GetAllRevisions();
				}
				repo.Update(shaOfUpdateFiles);
				currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
			}
			var updatefilesForThisSha = _liftUpdateScanner.GetUpdateFilesArrayForProjectAndSha(project, shaOfUpdateFiles);
			_lfSynchMerger.MergeUpdatesIntoFile(_lfDirectories.LiftFileMergePath(project), updatefilesForThisSha);
		}

		private void CloneProjectToMergerWorkFolder(string project, string projMergeFolder)
		{
			//Create the folder to clone the project into.
			_lfDirectories.CreateMergeWorkProjectFolder(project);
			Debug.Assert(Directory.Exists(projMergeFolder));
			//Get path for the repository then clone it to the webWork location
			var projWebFolder = _lfDirectories.GetProjWebPath(project);

			//Note: Why did this option get removed from HgRepository???
			//HgRepository.Clone(projWebFolder, projMergeFolder, new NullProgress());

			var sourceRepo = new HgRepository(projWebFolder, new NullProgress());
			sourceRepo.CloneLocalWithoutUpdate(projMergeFolder);
			var clonedRepoInMergeWork = new HgRepository(projMergeFolder, new NullProgress());
			clonedRepoInMergeWork.Update();
		}
	}
}
