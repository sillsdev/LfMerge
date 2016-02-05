// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using Autofac;
using Chorus.Model;
using LfMerge.Actions;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using LfMerge.Queues;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.IO.FileLock;
using SIL.Progress;


namespace LfMerge
{
	public class MainClass
	{
		public static IContainer Container { get; internal set; }

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<LfMergeSettingsIni>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<SyslogLogger>().SingleInstance().As<ILogger>()
				.WithParameter(new TypedParameter(typeof(string), "LfMerge"));
			containerBuilder.RegisterType<InternetCloneSettingsModel>().AsSelf();
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<UpdateBranchHelperFlex>().As<UpdateBranchHelperFlex>();
			containerBuilder.RegisterType<FlexHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<MongoConnection>().SingleInstance().As<IMongoConnection>().ExternallyOwned();
			containerBuilder.RegisterType<MongoProjectRecordFactory>().AsSelf();
			containerBuilder.RegisterType<ConsoleProgress>().AsSelf();
			LfMerge.Actions.Action.Register(containerBuilder);
			Queue.Register(containerBuilder);
			return containerBuilder;
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			if (Container == null)
				Container = RegisterTypes().Build();

			var logger = Container.Resolve<ILogger>();
			logger.Notice("LfMerge starting with args: {0}", string.Join(" ", args));

			var settings = Container.Resolve<LfMergeSettingsIni>();
			var fileLock = SimpleFileLock.CreateFromFilePath(settings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					logger.Notice("Can't acquire file lock - is another instance running?");
					return;
				}
				logger.Notice("Lock acquired");

				if (!EnsureSetup(settings, logger)) return;

				MongoConnection.Initialize(settings.MongoDbHostNameAndPort, settings.MongoMainDatabaseName);

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						logger.Notice("ProjectCode {0}", projectCode);
						var project = LanguageForgeProject.Create(settings, projectCode);
						EnsureClone(project, logger);

						queue.CurrentAction.Run(project);

						// TODO: Verify actions complete before dequeuing
						queue.DequeueProject(projectCode);
					}
				}
			}
			catch (Exception e)
			{
				logger.Debug("Unhandled Exception: \n" + e.ToString());
				throw;
			}
			finally
			{
				if (fileLock != null)
					fileLock.ReleaseLock();

				Container.Dispose();
				Cleanup();
			}

			logger.Notice("LfMerge finished\n");
		}

		protected static void EnsureClone(ILfProject project, ILogger logger)
		{
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var Progress = scope.Resolve<ConsoleProgress>();
				var model = scope.Resolve<InternetCloneSettingsModel>();
				if (project.LanguageDepotProject.Repository != null && project.LanguageDepotProject.Repository.Contains("private"))
					model.InitFromUri("http://hg-private.languagedepot.org");
				else
					model.InitFromUri("http://hg-public.languagedepot.org");

				var settings = Container.Resolve<LfMergeSettingsIni>();
				model.ParentDirectoryToPutCloneIn = settings.WebWorkDirectory;
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
						model.DoClone();
						if (!FinishClone(project))
							project.State.SRState = ProcessingState.SendReceiveStates.HOLD;

						LfMerge.Actions.Action.GetAction(ActionNames.UpdateMongoDbFromFdo).Run(project);
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

		private static bool FinishClone(ILfProject project)
		{
			var actualCloneResult = new ActualCloneResult();
			var Settings = Container.Resolve<LfMergeSettingsIni>();

			var cloneLocation = GetProjectDirectory(project.LfProjectCode, Settings);
			var newProjectFilename = Path.GetFileName(project.LfProjectCode) + SharedConstants.FwXmlExtension;
			var newFwProjectPathname = Path.Combine(cloneLocation, newProjectFilename);

			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var helper = scope.Resolve<UpdateBranchHelperFlex>();
				if (!helper.UpdateToTheCorrectBranchHeadIfPossible(
					MagicStrings.FDOModelVersion, actualCloneResult, cloneLocation))
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
				var Progress = scope.Resolve<ConsoleProgress>();
				projectUnifier.PutHumptyTogetherAgain(Progress, false, newFwProjectPathname);
				return true;
			}
		}

		/// <summary>
		/// Clean up anything needed before quitting, e.g. disposing of IDisposable objects.
		/// </summary>
		private static void Cleanup()
		{
			LanguageForgeProject.DisposeProjectCache();
		}

		private static bool EnsureSetup(LfMergeSettingsIni settings, ILogger logger)
		{
			var homeFolder = Environment.GetEnvironmentVariable("HOME") ?? "/var/www";
			string[] folderPaths = new[] { Path.Combine(homeFolder, ".local"),
				Path.GetDirectoryName(settings.WebWorkDirectory) };
			foreach (string folderPath in folderPaths)
			{
				if (!Directory.Exists(folderPath))
				{
					logger.Notice("Folder '{0}' doesn't exist", folderPath);
					return false;
				}
			}

			return true;
		}

	}
}
