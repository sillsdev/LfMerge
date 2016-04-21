// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Actions.Infrastructure;
using LfMerge.Settings;
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
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				GetAction(ActionNames.TransferMongoToFdo).Run(project);
				LanguageForgeProject.DisposeProjectCache(project.ProjectCode);

				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				LfMergeBridge.LfMergeBridge.DoSendReceive(
					Path.Combine(Settings.WebWorkDirectory, project.ProjectCode,
						project.ProjectCode + FdoFileHelper.ksFwDataXmlFileExtension),
					Progress, "Language Depot", chorusHelper.GetSyncUri(project));

				GetAction(ActionNames.TransferFdoToMongo).Run(project);
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}

	}
}
