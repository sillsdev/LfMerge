// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Autofac;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Settings;
using SIL.Code;
using SIL.LCModel;

namespace LfMerge.Core.Actions
{
	public class SynchronizeAction: Action
	{
		public SynchronizeAction(LfMergeSettings settings, LfMerge.Core.Logging.ILogger logger)
			: base(settings, logger)
		{
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		private bool FindErrorAndUpdateState(ILfProject project,
			string syncResult, string errorToFind, ProcessingState.SendReceiveStates? newState = null)
		{
			var line = LfMergeBridgeServices.GetLineContaining(syncResult, errorToFind);
			if (!string.IsNullOrEmpty(line))
			{
				if (line.Contains("Exception"))
				{
					 IEnumerable<string> stackTrace = LfMergeBridgeServices.GetLineAndStackTraceContaining(syncResult, errorToFind);
					 Logger.Error(String.Join(Environment.NewLine, stackTrace));  // We want entire stack trace logged as a single log entry, so don't use Logger.LogMany()
				}
				else
				{
					Logger.Error(line);
				}

				if (newState.HasValue)
				{
					if (newState.Value == ProcessingState.SendReceiveStates.HOLD)
					{
						// When going on hold, do so via the new PutOnHold() function so we record an error message
						project.State.PutOnHold("Error during synchronize of {0}: {1}",
							project.ProjectCode, line);
					}
					else
						project.State.SRState = newState.Value;
				}
				return true;
			}

			return false;
		}

		protected override void DoRun(ILfProject project)
		{
			using (MainClass.Container.BeginLifetimeScope())
			{
				var transferAction = GetAction(ActionNames.TransferMongoToLcm);
				transferAction.Run(project);

				int entriesAdded = 0, entriesModified = 0, entriesDeleted = 0;
				// Need to (safely) cast to TransferMongoToLcmAction to get the entry counts
				var transferMongoToLcmAction = transferAction as TransferMongoToLcmAction;
				if (transferMongoToLcmAction != null)
				{
					entriesAdded    = transferMongoToLcmAction.EntryCounts.Added;
					entriesModified = transferMongoToLcmAction.EntryCounts.Modified;
					entriesDeleted  = transferMongoToLcmAction.EntryCounts.Deleted;
				}

				Logger.Debug("About to dispose FW project {0}", project.ProjectCode);
				LanguageForgeProject.DisposeFwProject(project);
				Logger.Debug("Successfully disposed FW project {0}", project.ProjectCode);

				Logger.Notice("Syncing");
				string commitMessage = LfMergeBridgeServices.FormatCommitMessageForLfMerge(entriesAdded, entriesModified, entriesDeleted);
				if (commitMessage == null)  // Shouldn't happen, but be careful anyway
					commitMessage = "Language Forge Send/Receive";  // Desperate fallback

				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();

				// Call into LF Bridge to do the work.
				string syncResult;
				var options = new Dictionary<string, string>
				{
					{"fullPathToProject", project.ProjectDir},
					{"fwdataFilename", project.FwDataPath},
					{"fdoDataModelVersion", LcmCache.ModelVersion.ToString() },
					{"languageDepotRepoName", "Language Depot"},
					{"languageDepotRepoUri", chorusHelper.GetSyncUri(project)},
					{"commitMessage", commitMessage}
				};

				try {
					if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Send_Receive", Progress,
						options, out syncResult))
					{
						Logger.Error(syncResult);
						return;
					}
				} catch (System.FormatException e) {
					if (e.StackTrace.Contains("System.Int32.Parse")) {
						ChorusHelper.SetModelVersion(MagicStrings.MinimalModelVersionForNewBranchFormat);
						return;
					} else {
						throw;
					}
				}

				const string cannotCommitCurrentBranch = "Cannot commit to current branch '";
				var line = LfMergeBridgeServices.GetLineContaining(syncResult, cannotCommitCurrentBranch);
				if (!string.IsNullOrEmpty(line))
				{
					var index = line.IndexOf(cannotCommitCurrentBranch, StringComparison.Ordinal);
					Require.That(index >= 0);

					var modelVersion = int.Parse(line.Substring(index + cannotCommitCurrentBranch.Length, 7));
					if (modelVersion > MagicStrings.MaximalModelVersion) {
						// Chorus changed model versions to 75#####.xxxxxxx where xxxxxxx is the old-style model version
						modelVersion = int.Parse(line.Substring(index + cannotCommitCurrentBranch.Length + 8, 7));
					}
					if (modelVersion < MagicStrings.MinimalModelVersion)
					{
						FindErrorAndUpdateState(project, syncResult, cannotCommitCurrentBranch,
							ProcessingState.SendReceiveStates.HOLD);
						Logger.Error("Error during sync of '{0}': " +
							"clone model version '{1}' less than minimal supported model version '{2}'.",
							project.ProjectCode, modelVersion, MagicStrings.MinimalModelVersion);
						return;
					}
					ChorusHelper.SetModelVersion(modelVersion);
					return;
				}

				const string pulledHigherModel = "pulled a higher model '";
				line = LfMergeBridgeServices.GetLineContaining(syncResult, pulledHigherModel);
				if (!string.IsNullOrEmpty(line))
				{
					var index = line.IndexOf(pulledHigherModel, StringComparison.Ordinal);
					Require.That(index >= 0);

					var modelVersion = int.Parse(line.Substring(index + pulledHigherModel.Length, 7));
					ChorusHelper.SetModelVersion(modelVersion);

					// The .hg branch has a higher model version than the .fwdata file. We allow
					// data migrations and try again.
					Logger.Notice("Allow data migration for project '{0}' to migrate to model version '{1}'",
						project.ProjectCode, modelVersion);
					FwProject.AllowDataMigration = true;

					return;
				}

				if (FindErrorAndUpdateState(project, syncResult, "Cannot create a repository at this point in LF development.",
							ProcessingState.SendReceiveStates.HOLD) ||
						// REVIEW: should we set the state to HOLD if we don't have previous commits?
						FindErrorAndUpdateState(project, syncResult, "Cannot do first commit.",
							ProcessingState.SendReceiveStates.HOLD) ||
						FindErrorAndUpdateState(project, syncResult, "Sync failure:",
							ProcessingState.SendReceiveStates.HOLD))
				{
					return;
				}

				// Re-queue projects if clone failed due to DNS errors, as those are likely to be transient
				if (FindErrorAndUpdateState(project, syncResult,
						"Temporary failure in name resolution",
						ProcessingState.SendReceiveStates.CLONING))
				{
					LfMerge.Core.Queues.Queue.GetQueue(LfMerge.Core.Queues.QueueNames.Synchronize).EnqueueProject(project.ProjectCode);
					return;
				}

				line = LfMergeBridgeServices.GetLineContaining(syncResult, "No changes from others");
				if (!string.IsNullOrEmpty(line))
				{
					Logger.Notice(line);
					// We still need to transfer back to Mongo to delete any entries marked for deletion
				}
				else
				{
					// LfMergeBridge has code to detect when we got changes from others. However,
					// that code never executes because it does a pull before calling synchronizer.SyncNow()
					// so that syncResults.DidGetChangesFromOthers never gets set. It doesn't
					// matter to us because we always do a transfer to mongodb.
					line = LfMergeBridgeServices.GetLineContaining(syncResult, "Received changes from others");
					if (string.IsNullOrEmpty(line))
					{
						// Hmm. Bad news. Must have been some kind of problem down there.
						Logger.Error("Unhandled sync failure. Result we got was: {0}", syncResult);
						return;
					}
					Logger.Notice(line);
				}

				IAction transferLcmToMongoAction = GetAction(ActionNames.TransferLcmToMongo);
				if (transferLcmToMongoAction == null)
				{
					Logger.Error("Failed to run TransferLcmToMongo action: GetAction returned null");
					return;
				}
				transferLcmToMongoAction.Run(project);
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
