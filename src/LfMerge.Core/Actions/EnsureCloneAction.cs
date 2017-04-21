// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using Autofac;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using Palaso.Code;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.Actions
{
	/// <summary>
	/// Ensure that we have a clone of the repo.
	/// </summary>
	/// <remarks>This is a special action: it doesn't have a corresponding queue and will always
	/// be called. It will only do something if we don't have a clone yet.</remarks>
	public class EnsureCloneAction: Action
	{
		private ILfProject _currentProject;

		private MongoProjectRecordFactory _projectRecordFactory;

		private IMongoConnection _connection;

		public EnsureCloneAction(LfMergeSettings settings, ILogger logger, MongoProjectRecordFactory projectRecordFactory, IMongoConnection connection): base(settings, logger)
		{
			_projectRecordFactory = projectRecordFactory;
			_connection = connection;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return _currentProject.State.SRState; }
		}

		public override void PreRun(ILfProject project)
		{
			_currentProject = project;
			if (!File.Exists(Settings.GetStateFileName(_currentProject.ProjectCode)))
				_currentProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
		}

		private bool CloneResultedInError(ILfProject project,
			string cloneResult, string errorString)
		{
			var line = LfMergeBridgeServices.GetLineContaining(cloneResult, errorString);
			if (!string.IsNullOrEmpty(line))
			{
				Logger.Error("Error during initial clone of {0}: {1}", project.ProjectCode, line);
				project.State.PutOnHold("Error during initial clone of {0}: {1}", project.ProjectCode, line);
				return true;
			}
			return false;
		}

		protected virtual void InitialTransferToMongoAfterClone(ILfProject project)
		{
			Logger.Notice("Initial transfer to mongo after clone");
			project.IsInitialClone = true;
			Actions.Action.GetAction(ActionNames.TransferFdoToMongo).Run(project);
			project.IsInitialClone = false;
		}

		/// <summary>
		/// Dig out actual clone path from 'line'.
		/// </summary>
		private static string GetActualClonePath(string expectedCloneLocation, string line)
		{
			const string folder = "folder '";
			var folderIndex = line.IndexOf(folder, StringComparison.InvariantCulture);
			if (folderIndex >= 0)
			{
				var actualClonePath = line.Substring(folderIndex + folder.Length).TrimEnd('.').TrimEnd('\'');
				Require.That(expectedCloneLocation == actualClonePath,
					"Something changed in LfMergeBridge so that we cloned in a different directory");
				return actualClonePath;
			}
			return expectedCloneLocation;
		}

		private void ReportNoSuchBranchFailure(ILfProject project, string cloneLocation,
			string cloneResult, string line)
		{
			var clonePath = GetActualClonePath(cloneLocation, line);
			if (Directory.Exists(clonePath))
				Directory.Delete(clonePath, true);
			CloneResultedInError(project, cloneResult, "no such branch");
		}

		/// <summary>
		/// Ensures a Send/Receive project from Language Depot is properly
		/// cloned into the WebWork directory for LfMerge.Core.
		/// A project will be cloned if:
		/// 1) The LfMerge state file doesn't exist OR
		/// 2) The current LfMerge state is ProcessingState.SendReceiveStates.CLONING OR
		/// 3) The project directory is empty
		/// </summary>
		/// <param name="project">LF Project.</param>
		protected override void DoRun(ILfProject project)
		{
			try
			{
				// Check if an initial clone needs to be performed
				if (File.Exists(Settings.GetStateFileName(project.ProjectCode)) &&
					project.State.SRState != ProcessingState.SendReceiveStates.CLONING &&
					File.Exists(project.FwDataPath))
				{
					return;
				}
				var cloneLocation = project.ProjectDir;
				if (RepoAlreadyExists(cloneLocation))
					Logger.Notice("Repairing clone of project {0}", project.ProjectCode);
				else
					Logger.Notice("Initial clone for project {0}", project.ProjectCode);
				project.State.SRState = ProcessingState.SendReceiveStates.CLONING;

				string cloneResult;
				if (!CloneRepo(project, cloneLocation, out cloneResult))
				{
					Logger.Error(cloneResult);
					return;
				}

				if (CloneResultedInError(project, cloneResult, "clone is not a FLEx project") ||
					CloneResultedInError(project, cloneResult, "new repository with no commits") ||
					CloneResultedInError(project, cloneResult, "clone has higher model") ||
					CloneResultedInError(project, cloneResult, "LfMergeBridge starting S/R handler from directory"))
				{
					return;
				}

				string line = LfMergeBridgeServices.GetLineContaining(cloneResult, "no such branch");
				if (!string.IsNullOrEmpty(line))
				{
					const string modelString = "Highest available model '";
					var index = line.IndexOf(modelString, StringComparison.Ordinal);
					if (index < 0)
					{
						ReportNoSuchBranchFailure(project, cloneLocation, cloneResult, line);
						return;
					}

					var cloneModelVersion = line.Substring(index + modelString.Length, 7);
					if (int.Parse(cloneModelVersion) < int.Parse(MagicStrings.MinimalModelVersion))
					{
						ReportNoSuchBranchFailure(project, cloneLocation, cloneResult, line);
						Logger.Error("Error during initial clone of '{0}': " +
							"clone model version '{1}' less than minimal supported model version '{2}'.",
							project.ProjectCode, cloneModelVersion, MagicStrings.MinimalModelVersion);
						return;
					}
					Logger.Info(line);
					ChorusHelper.SetModelVersion(cloneModelVersion);
					ChorusHelper.SetTreatAsInitialClone(true);
				}
				else
				{
					ChorusHelper.SetModelVersion(FdoCache.ModelVersion);
					line = LfMergeBridgeServices.GetLineContaining(cloneResult,
						"new clone created on branch");
					Require.That(!string.IsNullOrEmpty(line),
						"Looks like the clone was not successful, but we didn't get an understandable error");

					// verify clone path
					GetActualClonePath(cloneLocation, line);

					if (!ChorusHelper.ThisIsAnInitialClone && MongoProjectHasUserDataOrHasBeenSynced())
					{
						// If the local Mercurial repo was deleted but the Mongo database is still there,
						// then there might be data in Mongo that we still need, in which case we should NOT
						// skip the syncing step. So do nothing, so that we'll fall through to the SYNCING state.
						Logger.Debug("This appears to NOT be an initial clone");
					}
					else
					{
						if (MongoProjectHasUserDataOrHasBeenSynced())
							Logger.Debug("Even though the project exists and is non-empty, we're calling this an initial clone anyway because of the command-line flag");
						else
							Logger.Debug("This is an initial clone both because the flag was set AND because the project appears to be empty or non-existent");
						InitialTransferToMongoAfterClone(project);
						Logger.Notice("Initial clone completed; setting state to CLONED");
						project.State.SRState = ProcessingState.SendReceiveStates.CLONED;
					}
				}
			}
			catch (Exception e)
			{
				if (e.GetType().Name == "ArgumentOutOfRangeException" &&
					e.Message == "Cannot update to any branch.")
				{
					project.State.PutOnHold("Error during initial clone of {0}: {1}",
						project.ProjectCode, e);
					throw;
				}
				if (e.GetType().Name == "RepositoryAuthorizationException")
				{
					Logger.Error("Initial clone of {0}: authorization exception", project.ProjectCode);
					project.State.PutOnHold(
						"Error during initial clone of {0}: authorization exception from remote repository",
						project.ProjectCode);
					throw;
				}
				Logger.Error("Got {0} exception trying to clone {1}: {2}", e.GetType(),
					project.ProjectCode, e.Message);
				throw;
			}
		}

		private static bool RepoAlreadyExists(string projectFolderPath)
		{
			return Directory.Exists(projectFolderPath);
		}

		private bool MongoProjectHasUserDataOrHasBeenSynced()
		{
			MongoProjectRecord record = _projectRecordFactory.Create(_currentProject);
			bool projectHasBeenSynced = record.LastSyncedDate != null && record.LastSyncedDate > MagicValues.UnixEpoch;
			long projectEntryCount = _connection.EntryCount(_currentProject);
			return record != null && ! string.IsNullOrEmpty(record.SendReceiveProjectIdentifier) && (projectHasBeenSynced || projectEntryCount > 0);
		}

		protected virtual bool CloneRepo(ILfProject project, string projectFolderPath,
			out string cloneResult)
		{
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			var options = new Dictionary<string, string> {
				{ "fullPathToProject", projectFolderPath },
				{ "languageDepotRepoName", project.LanguageDepotProject.Identifier },
				{ "fdoDataModelVersion", FdoCache.ModelVersion },
				{ "languageDepotRepoUri", chorusHelper.GetSyncUri(project) },
				{ "user", "Language Forge" },
				{ "deleteRepoIfNoSuchBranch", "false" },
				{ "onlyRepairRepo", RepoAlreadyExists(projectFolderPath) ? "true" : "false" }
			};
			return LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Clone", Progress, options,
				out cloneResult);
		}
	}
}

