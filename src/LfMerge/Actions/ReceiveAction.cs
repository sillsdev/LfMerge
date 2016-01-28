// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.Model;
using LfMerge.FieldWorks;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.Progress;
using SIL.Reporting;

namespace LfMerge.Actions
{
	public class ReceiveAction: Action
	{
		public ReceiveAction(LfMergeSettingsIni settings, LfMerge.Logging.ILogger logger) : base(settings, logger) {}

		private IProgress Progress { get; set; }

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.RECEIVING; }
		}

		protected override void DoRun(ILfProject project)
		{
			Progress = new ConsoleProgress();
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var model = scope.Resolve<InternetCloneSettingsModel>();
				model.InitFromUri("http://hg-public.languagedepot.org");	// TODO: check mongo project for private repo
				model.ParentDirectoryToPutCloneIn = Settings.WebWorkDirectory;
				model.AccountName = project.LanguageDepotProject.Username;
				model.Password = project.LanguageDepotProject.Password;
				model.ProjectId = project.LanguageDepotProject.ProjectCode;
				model.LocalFolderName = project.LfProjectCode;
				model.AddProgress(Progress);

				try
				{
					if (!Directory.Exists(model.ParentDirectoryToPutCloneIn) ||
						model.TargetLocationIsUnused)
					{
						InitialClone(model);
						if (!FinishClone(project))
							project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					}
				}
				catch (UnauthorizedAccessException)
				{
					project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					throw;
				}
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.Merge; }
		}

		private static string GetProjectDirectory(string projectCode, LfMergeSettingsIni settings)
		{
			return Path.Combine(settings.WebWorkDirectory, projectCode);
		}

		private void InitialClone(InternetCloneSettingsModel model)
		{
			model.DoClone();
		}

		private bool FinishClone(ILfProject project)
		{
			var actualCloneResult = new ActualCloneResult();

			var cloneLocation = GetProjectDirectory(project.LfProjectCode, Settings);
			var newProjectFilename = Path.GetFileName(project.LfProjectCode) + SharedConstants.FwXmlExtension;
			var newFwProjectPathname = Path.Combine(cloneLocation, newProjectFilename);

			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var helper = scope.Resolve<UpdateBranchHelperFlex>();
				if (!helper.UpdateToTheCorrectBranchHeadIfPossible(
				/*FDOBackendProvider.ModelVersion*/ "7000068", actualCloneResult, cloneLocation))
				{
					actualCloneResult.Message = "Flex version is too old";
				}

				switch (actualCloneResult.FinalCloneResult)
				{
					case FinalCloneResult.ExistingCloneTargetFolder:
						SIL.Reporting.Logger.WriteEvent("Clone failed: Flex project exists: {0}", cloneLocation);
						if (Directory.Exists(cloneLocation))
							Directory.Delete(cloneLocation, true);
						return false;
					case FinalCloneResult.FlexVersionIsTooOld:
						SIL.Reporting.Logger.WriteEvent("Clone failed: Flex version is too old; project: {0}",
							project.LfProjectCode);
						if (Directory.Exists(cloneLocation))
							Directory.Delete(cloneLocation, true);
						return false;
					case FinalCloneResult.Cloned:
						break;
				}

				var projectUnifier = scope.Resolve<FlexHelper>();
				projectUnifier.PutHumptyTogetherAgain(Progress, false, newFwProjectPathname);
				return true;
			}
		}
	}
}

