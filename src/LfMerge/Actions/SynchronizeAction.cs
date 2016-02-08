// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Settings;
using Chorus.sync;
using SIL.Progress;

namespace LfMerge.Actions
{
	public class SynchronizeAction: Action
	{
		public SynchronizeAction(LfMergeSettingsIni settings, LfMerge.Logging.ILogger logger) : base(settings, logger) {}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.RECEIVING; }
		}

		protected override void DoRun(ILfProject project)
		{
			Logger.Notice("LfMerge: starting Sync");

			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				// TODO: Add this back in once we verify UpdateMongoDbFromFdo working
				//GetAction(ActionNames.UpdateFdoFromMongoDb).Run(project);
				/*
				var projectFolderPath = Path.Combine(Settings.WebWorkDirectory, project.LfProjectCode);
				var config = new ProjectFolderConfiguration(projectFolderPath);
				var synchroniser = Synchronizer.FromProjectConfiguration(config, Progress);
				var options = new SyncOptions();
				synchroniser.SyncNow(options);
*/
				GetAction(ActionNames.UpdateMongoDbFromFdo).Run(project);
			}

			Logger.Notice("LfMerge: done with Sync");
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}

	}
}
