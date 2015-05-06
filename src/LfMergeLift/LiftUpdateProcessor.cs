// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers;
using Chorus.VcsDrivers.Mercurial;
using System.Windows.Forms;
using Chorus.sync;
using Palaso.Progress;

namespace LfMergeLift
{
	/// <summary>
	/// Changes made by Language Forge users will cause .lift.update files to be stored in the /mergeWork/updates folder on the server.
	/// This class ensures that these .lift.update files are merged into the .lift file for the correct sha and also does Mercurial operations
	/// to do synchronizations and commits when needed.
	///
	/// Lift Update files from Language Forge have the following format:    ProjX_sha_timeStamp.lift.update
	/// </summary>
	class LiftUpdateProcessor
	{
		private HgRepository _hgRepo;
		private LiftUpdatesScanner _liftUpdateScanner;
		private LfSynchronicMerger _lfSynchMerger;
		private LfDirectoriesAndFiles _lfDirectories;
		private String LangForgeVersion = "1.0";

		private bool _commitWasDoneForProject;
		private bool _liftUpdatesWereAppliedToProject;

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

		public LiftUpdatesScanner LiftUpdateScanner
		{
			get { return _liftUpdateScanner; }
		}

		public bool CommitWasDone
		{
			get { return _commitWasDoneForProject; }
		}

		public bool LiftUpdatesWereApplied
		{
			get { return _liftUpdatesWereAppliedToProject; }
		}

		public void ProcessLiftUpdates()
		{
			//If there are .lift.update files to be processed then do nothing
			if (!LiftUpdateScanner.ScannerHasListOfLiftUpdates)
				return;

			//Get the projects which need to have .lift.updates applied to them
			var projects = LiftUpdateScanner.GetProjectsNamesToUpdate();
			foreach (var project in projects)
			{
				ProcessLiftUpdatesForProject(project);
			}
		}

		//make internal for tests.
		internal void ProcessLiftUpdatesForProject(string project)
		{
			_commitWasDoneForProject = false;
			_liftUpdatesWereAppliedToProject = false;
			var projMergeFolder = _lfDirectories.GetProjMergePath(project);
			//first check if the project has been cloned to the mergeWork folder. If not then clone it.
			if (!Directory.Exists(projMergeFolder))
			{
				CloneProjectToMergerWorkFolder(project, projMergeFolder);
			}

			var repo = new HgRepository(projMergeFolder, new NullProgress()); //
			var shas = LiftUpdateScanner.GetShasForAProjectName(project);
			//foreach sha, apply all the updates. We may need to change to that sha on the repo so check for this
			//before doing the lfSynchronicMerger
			foreach (String sha in shas)
			{
				ProcessUpdatesForAParticularSha(project, repo, sha);
			}
			if (_liftUpdatesWereAppliedToProject)
			{
				//Then copy the project .lift file to the webwork folder location
				File.Copy(_lfDirectories.LiftFileMergePath(project), _lfDirectories.LiftFileWebWorkPath(project), true);
			}
			if (_commitWasDoneForProject)
			{
				//Then do a send/receive with the webwork Mercurial repo and the master repo too.
				var webWorkRepoAddress = RepositoryAddress.Create(project, _lfDirectories.GetProjWebPath(project));
				repo.Pull(webWorkRepoAddress, "");
				repo.Push(webWorkRepoAddress, "");

				var masterRepoAddress = RepositoryAddress.Create(project, _lfDirectories.GetProjMasterRepoPath(project));
				repo.Pull(masterRepoAddress, "");
				repo.Push(masterRepoAddress, "");
			}
		}

		//make internal for tests.
		internal void ProcessUpdatesForAParticularSha(string project, HgRepository repo, string shaOfUpdateFiles)
		{
			String currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
			var allShas = repo.GetAllRevisions();
			if (!(currentSha.Equals(shaOfUpdateFiles)))
			{
				_commitWasDoneForProject = true;
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
					{
						throw new ApplicationException(String.Format("Merge failure while processing updates on Project:{0} and Sha:{1}", project, shaOfUpdateFiles));
					}
					allShas = repo.GetAllRevisions();
				}
				repo.Update(shaOfUpdateFiles);
				currentSha = repo.GetRevisionWorkingSetIsBasedOn().Number.Hash;
			}
			var updatefilesForThisSha = LiftUpdateScanner.GetUpdateFilesArrayForProjectAndSha(project, shaOfUpdateFiles);
			if (updatefilesForThisSha.Count() > 0)
				_liftUpdatesWereAppliedToProject = true;
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
