// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using LfMerge.Actions.Infrastructure;
using LfMerge.Settings;

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
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				GetAction(ActionNames.TransferMongoToFdo).Run(project);
				LanguageForgeProject.DisposeProjectCache(project.ProjectCode);

				var dataProvider = new LfMergeBridgeDataProvider(Settings,
					(LfMergeBridge.ILogger)Logger, Progress, project);

				using (var bridge = new LfMergeBridge.LfMergeBridge(dataProvider))
				{
					bridge.SendReceive();
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
