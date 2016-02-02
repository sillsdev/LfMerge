// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using Autofac;
using Chorus.Model;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.Progress;

namespace LfMerge.Actions
{
	public abstract class Action: IAction
	{
		protected LfMergeSettingsIni Settings { get; set; }
		protected ILogger Logger { get; set; }

		private IProgress Progress { get; set; }

		#region Action handling
		internal static IAction GetAction(ActionNames actionName)
		{
			var action = MainClass.Container.ResolveKeyed<IAction>(actionName);
			var actionAsAction = action as Action;
			if (actionAsAction != null)
				actionAsAction.Name = actionName;
			return action;
		}

		internal static void Register(ContainerBuilder containerBuilder)
		{
			containerBuilder.RegisterType<CommitAction>().Keyed<IAction>(ActionNames.Commit).SingleInstance();
			containerBuilder.RegisterType<EditAction>().Keyed<IAction>(ActionNames.Edit).SingleInstance();
			containerBuilder.RegisterType<SynchronizeAction>().Keyed<IAction>(ActionNames.Synchronize).SingleInstance();
			containerBuilder.RegisterType<UpdateFdoFromMongoDbAction>().Keyed<IAction>(ActionNames.UpdateFdoFromMongoDb).SingleInstance();
			containerBuilder.RegisterType<UpdateMongoDbFromFdo>().Keyed<IAction>(ActionNames.UpdateMongoDbFromFdo).SingleInstance();
		}

		#endregion

		public Action(LfMergeSettingsIni settings, ILogger logger)
		{
			Settings = settings;
			Logger = logger;
		}

		protected abstract ProcessingState.SendReceiveStates StateForCurrentAction { get; }

		protected abstract ActionNames NextActionName { get; }

		protected abstract void DoRun(ILfProject project);

		#region IAction implementation

		public ActionNames Name { get; private set; }

		public IAction NextAction
		{
			get
			{
				return NextActionName != ActionNames.None ? GetAction(NextActionName) : null;
			}
		}

		public void Run(ILfProject project)
		{
			Logger.Notice("Action {0} just started", Name);

			if (project.State.SRState == ProcessingState.SendReceiveStates.HOLD)
				return;

			project.State.SRState = StateForCurrentAction;
			try
			{
				EnsureClone(project);
				DoRun(project);
			}
			// REVIEW: catch any exception and set state to hold?
			// TODO: log exceptions
			finally
			{
				if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;
			}
		}

		#endregion

		protected void EnsureClone(ILfProject project)
		{
			Progress = new ConsoleProgress();
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var model = scope.Resolve<InternetCloneSettingsModel>();
				if (project.LanguageDepotProject.Repository != null && project.LanguageDepotProject.Repository.Contains("private"))
					model.InitFromUri("http://hg-private.languagedepot.org");
				else
					model.InitFromUri("http://hg-public.languagedepot.org");

				model.ParentDirectoryToPutCloneIn = Settings.WebWorkDirectory;
				model.AccountName = project.LanguageDepotProject.Username;
				model.Password = project.LanguageDepotProject.Password;
				model.ProjectId = project.LanguageDepotProject.Identifier;
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
				catch (Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException)
				{
					project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					throw;
				}
			}
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

