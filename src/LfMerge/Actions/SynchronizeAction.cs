// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Collections.Generic;
using Autofac;
using LfMerge.Actions.Infrastructure;
using LfMerge.Settings;
using Palaso.Progress;
using SIL.FieldWorks.FDO;

namespace LfMerge.Actions
{
	public class SynchronizeAction: Action
	{
		public SynchronizeAction(LfMergeSettingsIni settings, LfMerge.Logging.ILogger logger)
			: base(settings, logger)
		{
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		protected override void DoRun(ILfProject project)
		{
			using (MainClass.Container.BeginLifetimeScope())
			{
				GetAction(ActionNames.TransferMongoToFdo).Run(project);
				LanguageForgeProject.DisposeFwProject(project);

				Logger.Notice("Syncing");

				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				var projectFolderPath = Path.Combine(Settings.WebWorkDirectory, project.ProjectCode);

				// Call into LF Bridge to do the work.
				string syncResult;
				var options = new Dictionary<string, string>
				{
					{"projectPath", projectFolderPath},
					{"fwdataFilename", project.ProjectCode + FdoFileHelper.ksFwDataXmlFileExtension},
					{"languageDepotRepoName", "Language Depot"},
					{"languageDepotRepoUri", chorusHelper.GetSyncUri(project)}
				};
				LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Send_Receive", Progress, options, out syncResult);

				if (syncResult.Contains("Sync failed: Cannot create a repository at this point in LF development."))
				{
					Logger.Error("Sync failed: Cannot create a repository at this point in LF development.");
					return;
				}
				var line = LfMergeBridgeServices.GetLineStartingWith(syncResult, "Sync failed - ");
				if (!string.IsNullOrEmpty(line))
				{
					Logger.Error(line);
					return;
				}
				line = LfMergeBridgeServices.GetLineFromLfBridge(syncResult, "No changes from others");
				if (!string.IsNullOrEmpty(line))
				{
					Logger.Notice(line);
					// We still need to transfer back to Mongo to delete any entries marked for deletion
				}
				else
				{
					line = LfMergeBridgeServices.GetLineFromLfBridge(syncResult, "Received changes from others");
					if (string.IsNullOrEmpty(line))
					{
						// Hmm. Bad news. Must have been some kind of problem down there.
						Logger.Error("Unknown sync failure.");
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
