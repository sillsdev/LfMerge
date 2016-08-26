// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Autofac;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Settings;
using SIL.FieldWorks.FDO;

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

		private bool SyncResultedInError(ILfProject project,
			string syncResult, string errorString, ProcessingState.SendReceiveStates? newState = null)
		{
			var line = LfMergeBridgeServices.GetLineContaining(syncResult, errorString);
			if (!string.IsNullOrEmpty(line))
			{
				Logger.Error(line);
				if (newState.HasValue)
					project.State.SRState = newState.Value;
				return true;
			}
			return false;
		}

		protected override void DoRun(ILfProject project)
		{
			using (MainClass.Container.BeginLifetimeScope())
			{
				var transferAction = GetAction(ActionNames.TransferMongoToFdo);
				transferAction.Run(project);
				LanguageForgeProject.DisposeFwProject(project);

				int entriesAdded = 0, entriesModified = 0, entriesDeleted = 0;
				if (transferAction is TransferMongoToFdoAction)
				{
					// Need to (safely) cast to TransferMongoToFdoAction to get the entry counts
					var action = (TransferMongoToFdoAction)transferAction;
					entriesAdded    = action.EntryCounts.Added;
					entriesModified = action.EntryCounts.Modified;
					entriesDeleted  = action.EntryCounts.Deleted;
				}
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
					{"fdoDataModelVersion", FdoCache.ModelVersion },
					{"languageDepotRepoName", "Language Depot"},
					{"languageDepotRepoUri", chorusHelper.GetSyncUri(project)},
					{"commitMessage", commitMessage}
				};
				if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Send_Receive", Progress,
					options, out syncResult))
				{
					Logger.Error(syncResult);
					return;
				}

				if (SyncResultedInError(project, syncResult,
						"Cannot create a repository at this point in LF development.",
						ProcessingState.SendReceiveStates.HOLD) ||
					// REVIEW: should we set the state to HOLD if we don't have previous commits?
					SyncResultedInError(project, syncResult, "Cannot do first commit.",
						ProcessingState.SendReceiveStates.HOLD) ||
					SyncResultedInError(project, syncResult, "Sync failure:"))
				{
					return;
				}

				var line = LfMergeBridgeServices.GetLineContaining(syncResult, "No changes from others");
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

				GetAction(ActionNames.TransferFdoToMongo).Run(project);
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
