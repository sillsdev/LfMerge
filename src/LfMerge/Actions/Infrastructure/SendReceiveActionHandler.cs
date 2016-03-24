// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using Autofac;
using Chorus;
using Chorus.sync;
using Chorus.UI.Sync;
using Chorus.VcsDrivers;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure.ActionHandlers;
using LibTriboroughBridgeChorusPlugin;
using Palaso.Network;
using Palaso.Progress;

namespace LfMerge.Actions.Infrastructure
{
	public class SendReceiveActionHandler: SendReceiveAction
	{
		public SendReceiveActionHandler(LfMergeSettingsIni settings, LfMerge.Logging.ILogger logger,
			IProgress progress, ILfProject project)
		{
			Settings = settings;
			Logger = logger;
			Progress = progress;
			Project = project;
		}

		private LfMergeSettingsIni Settings { get; set; }
		private LfMerge.Logging.ILogger Logger { get; set; }
		private IProgress Progress { get; set; }
		private ILfProject Project { get; set; }

		protected override ChorusSystemSimple InitializeChorusSystem(string directoryName,
			string user)
		{
			var chorusSystem = base.InitializeChorusSystem(directoryName, user);

			var builder = new ContainerBuilder();
			builder.Register<ProjectFolderConfiguration>(
				c => new ProjectFolderConfiguration(ProjectFolderPath)).InstancePerLifetimeScope();
			builder.Register<IProgress>(
				c => Progress).InstancePerLifetimeScope();
			builder.Update(chorusSystem.Container);

			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			string applicationName = assemblyName.Name;
			string applicationVersion = assemblyName.Version.ToString();

			var controlModel = chorusSystem.Container.Resolve<SyncControlModelSimple>();
			var syncOptions = controlModel.SyncOptions;
			syncOptions.CheckinDescription = string.Format("[{0}: {1}] sync",
				applicationName, applicationVersion);
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			syncOptions.RepositorySourcesToTry.Add(
				RepositoryAddress.Create("Language Depot", chorusHelper.GetSyncUri(Project)));

			return chorusSystem;
		}

		private string ProjectFolderPath
		{
			get
			{
				return Path.Combine(Settings.WebWorkDirectory, Project.ProjectCode);
			}
		}

		private SyncResults RunSync(ChorusSystemSimple chorusSystem)
		{
			var controlModel = chorusSystem.Container.Resolve<SyncControlModelSimple>();

			var fwdataPathname = Path.Combine(ProjectFolderPath,
				Project.ProjectCode + SharedConstants.FwXmlExtension);

			var synchroniser = Synchronizer.FromProjectConfiguration(
				chorusSystem.ProjectFolderConfiguration, Progress);
			synchroniser.SynchronizerAdjunct = new LfMergeSychronizerAdjunct(fwdataPathname,
				MagicStrings.FwFixitAppName, true); // Settings.VerboseProgress);

			return synchroniser.SyncNow(controlModel.SyncOptions);
		}

		public void StartWorking()
		{
			// Syncing of a new repo is not currently supported.
			// For implementation, look in ~/fwrepo/flexbridge/src/FLEx-ChorusPlugin/Infrastructure/ActionHandlers/SendReceiveActionHandler.cs
			Logger.Notice("Syncing");

			var result = Run(ProjectFolderPath, Project.LanguageDepotProject.Username, RunSync);

			if (!result.Succeeded)
			{
				Logger.Error("Sync failed - {0}", result.ErrorEncountered);
				return;
			}
			if (result.DidGetChangesFromOthers)
				Logger.Notice("Received changes from others");
			else
				Logger.Notice("No changes from others");
		}
	}
}

