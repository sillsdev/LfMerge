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

				// Syncing of a new repo is not currently supported.
				// For implementation, look in ~/fwrepo/flexbridge/src/FLEx-ChorusPlugin/Infrastructure/ActionHandlers/SendReceiveActionHandler.cs
				Logger.Notice("Syncing");

				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				var projectFolderPath = Path.Combine(Settings.WebWorkDirectory, project.ProjectCode);

				// Call into LF Bridge to do the work.
				string somethingForClient;
				var options = new Dictionary<string, string>
				{
					{"projectPath", projectFolderPath},
					{"fwdataFilename", project.ProjectCode + ".fwdata"},
					{"languageDepotRepoName", "Language Depot"},
					{"languageDepotRepoUri", chorusHelper.GetSyncUri(project)}
				};
				LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Send_Receive", Progress, options, out somethingForClient);

// REVIEW Eberhard(RandyR): What kind of states belong with each of these 'line' checks, if any?
				// LF Bridge may have thrown an exception (in which case we won't get to this line),
				// or it may have done something. The most important to us 'somethings' will be in 'somethingForClient'.
				if (somethingForClient.Contains("Sync failed: Cannot create a repository at this point in LF development."))
				{
					Logger.Error("Sync failed: Cannot create a repository at this point in LF development.");
					return;
				}
				var line = LanguageForgeBridgeServices.GetLineStartingWith(somethingForClient, "Sync failed - ");
				if (!string.IsNullOrEmpty(line))
				{
					Logger.Error(line);
					return;
				}
				line = LanguageForgeBridgeServices.GetLineFromLfBridge(somethingForClient, "No changes from others");
				if (!string.IsNullOrEmpty(line))
				{
					Logger.Notice(line);
					return;
				}
				line = LanguageForgeBridgeServices.GetLineFromLfBridge(somethingForClient, "Received changes from others");
				if (string.IsNullOrEmpty(line))
				{
					// Hmm. Bad news. Must have been some kind of problem down there.
					Logger.Error("Unknown sync failure.");
					return;
				}

				// At this point, we know we have new stuff from afar, so wipe out FdoCache, reload it, and send it back to Mongo.
				Logger.Notice(line);
				LanguageForgeProject.DisposeProjectCache(project.ProjectCode);
				GetAction(ActionNames.TransferFdoToMongo).Run(project);
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
