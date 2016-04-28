// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Settings;
using Palaso.Progress;
using System;
using System.IO;
using System.Reflection;

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
#if GETTING_RID_OF_FB_AND_CHORUS
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				GetAction(ActionNames.TransferMongoToFdo).Run(project);
				LanguageForgeProject.DisposeProjectCache(project.ProjectCode);

				// Syncing of a new repo is not currently supported.
				// For implementation, look in ~/fwrepo/flexbridge/src/FLEx-ChorusPlugin/Infrastructure/ActionHandlers/SendReceiveActionHandler.cs
				Logger.Notice("Syncing");
// GETTING_RID_OF_FB_AND_CHORUS TODO: Pass the buck to LfBridge in FB.
				string applicationName = Assembly.GetExecutingAssembly().GetName().Name;
				string applicationVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				var repoPath = RepositoryAddress.Create("Language Depot", chorusHelper.GetSyncUri(project));
				var syncOptions = new SyncOptions();
				syncOptions.CheckinDescription = "[" + applicationName  + ": " + applicationVersion + "] sync";
				syncOptions.RepositorySourcesToTry.Add(repoPath);
				string projectFolderPath = Path.Combine(Settings.WebWorkDirectory, project.ProjectCode);
				var projectConfig = new ProjectFolderConfiguration(projectFolderPath);
// GETTING_RID_OF_FB_AND_CHORUS TODO: Restore FlexFolderSystem class to be internal, as well as its ConfigureChorusProjectFolder static method.
				FlexFolderSystem.ConfigureChorusProjectFolder(projectConfig);
				var synchroniser = Synchronizer.FromProjectConfiguration(projectConfig, Progress);
// GETTING_RID_OF_FB_AND_CHORUS TODO: Restore SharedConstants and what it holds to be internal.
				string fwdataFilePath = Path.Combine(projectFolderPath, project.ProjectCode + SharedConstants.FwXmlExtension);
				synchroniser.SynchronizerAdjunct = new LfMergeSychronizerAdjunct(fwdataFilePath, MagicStrings.FwFixitAppName, true); // Settings.VerboseProgress);
				SyncResults syncResult = synchroniser.SyncNow(syncOptions);
// GETTING_RID_OF_FB_AND_CHORUS TODO: Call LFBridge method here with what it needs.
				if (!syncResult.Succeeded)
				{
					Logger.Error("Sync failed - {0}", syncResult.ErrorEncountered);
					return;
				}
				if (syncResult.DidGetChangesFromOthers)
					Logger.Notice("Received changes from others");
				else
					Logger.Notice("No changes from others");

				GetAction(ActionNames.TransferFdoToMongo).Run(project);
			}
#else
			throw new NotImplementedException("Needs to be redone as Chorus-free and only using LFBridge FB assembly.");
#endif
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	
	}
}
