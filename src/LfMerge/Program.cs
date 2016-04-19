// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using Autofac;
using Chorus.Model;
using LfMerge.Actions;
using LfMerge.Actions.Infrastructure;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using LfMerge.Queues;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using Palaso.IO.FileLock;
using Palaso.Progress;
using SIL.FieldWorks.FDO;


namespace LfMerge
{
	public class MainClass
	{
		public static IContainer Container { get; internal set; }
		public static ILogger Logger { get; set; }

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<LfMergeSettingsIni>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<SyslogLogger>().SingleInstance().As<ILogger>()
				.WithParameter(new TypedParameter(typeof(string), "LfMerge"));
			containerBuilder.RegisterType<InternetCloneSettingsModel>().AsSelf();
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<ChorusHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<UpdateBranchHelperFlex>().As<UpdateBranchHelperFlex>();
			containerBuilder.RegisterType<FlexHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<MongoConnection>().SingleInstance().As<IMongoConnection>().ExternallyOwned();
			containerBuilder.RegisterType<MongoProjectRecordFactory>().AsSelf();
			containerBuilder.RegisterType<SyslogProgress>().As<IProgress>();
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

			Logger = Container.Resolve<ILogger>();
			Logger.Notice("LfMerge starting with args: {0}", string.Join(" ", args));

			var settings = Container.Resolve<LfMergeSettingsIni>();
			var fileLock = SimpleFileLock.CreateFromFilePath(settings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					Logger.Error("Can't acquire file lock - is another instance running?");
					return;
				}
				Logger.Notice("Lock acquired");

				if (!CheckSetup(settings)) return;

				MongoConnection.Initialize(settings.MongoDbHostNameAndPort, settings.MongoMainDatabaseName);

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						Logger.Notice("ProjectCode {0}", projectCode);
						var project = LanguageForgeProject.Create(settings, projectCode);
						EnsureClone(project);

						queue.CurrentAction.Run(project);

						if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
							project.State.SRState = ProcessingState.SendReceiveStates.IDLE;

						// TODO: Verify actions complete before dequeuing
						queue.DequeueProject(projectCode);
					}
				}
			}
			catch (Exception e)
			{
				Logger.Debug("Unhandled Exception: \n{0}", e);
				throw;
			}
			finally
			{
				if (fileLock != null)
					fileLock.ReleaseLock();

				Container.Dispose();
				Cleanup();
			}

			Logger.Notice("LfMerge finished");
		}

		/// <summary>
		/// Ensures a Send/Receive project from Language Depot is properly
		/// cloned into the WebWork directory for LfMerge.
		/// A project will be cloned if:
		/// 1) The LfMerge state file doesn't exist OR
		/// 2) The current LfMerge state is ProcessingState.SendReceiveStates.CLONING OR
		/// 3) The project directory is empty
		/// </summary>
		/// <param name="project">LF Project.</param>
		public static void EnsureClone(ILfProject project)
		{
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var progress = scope.Resolve<IProgress>();
				var settings = Container.Resolve<LfMergeSettingsIni>();
				var model = scope.Resolve<InternetCloneSettingsModel>();
				model.InitFromUri(project.LanguageDepotProjectUri);
				model.ParentDirectoryToPutCloneIn = settings.WebWorkDirectory;
				model.AccountName = project.LanguageDepotProject.Username;
				model.Password = project.LanguageDepotProject.Password;
				model.ProjectId = project.LanguageDepotProject.Identifier;
				model.LocalFolderName = project.ProjectCode;
				model.AddProgress(progress);

				try
				{
					// Check if an initial clone needs to be performed
					if (!File.Exists(settings.GetStateFileName(project.ProjectCode)) ||
						(project.State.SRState == ProcessingState.SendReceiveStates.CLONING) ||
						model.TargetLocationIsUnused)
					{
						Logger.Notice("Initial clone");
						// Since we're in here, the previous clone was not finished, so remove and start over
						var cloneLocation = Path.Combine(settings.WebWorkDirectory, project.ProjectCode);
						if (Directory.Exists(cloneLocation))
						{
							Logger.Notice("Cleaning out previous failed clone at {0}", cloneLocation);
							Directory.Delete(cloneLocation, true);
						}
						project.State.SRState = ProcessingState.SendReceiveStates.CLONING;
						model.DoClone();
						if (model.LocalFolderName != project.ProjectCode)
							Logger.Notice("Warning: Folder {0} already exists, so project cloned in {1}", project.ProjectCode, model.TargetDestination);
						if (!FinishClone(project))
							project.State.SRState = ProcessingState.SendReceiveStates.HOLD;

						Logger.Notice("Initial transfer to mongo after clone");
						project.IsInitialClone = true;
						LfMerge.Actions.Action.GetAction(ActionNames.TransferFdoToMongo).Run(project);
						project.IsInitialClone = false;
					}
				}
				catch (Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException)
				{
					Logger.Error("Initial clone authorization exception");
					project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					throw;
				}
			}
		}

		private static bool FinishClone(ILfProject project)
		{
			var actualCloneResult = new ActualCloneResult();
			var settings = Container.Resolve<LfMergeSettingsIni>();

			var cloneLocation = Path.Combine(settings.WebWorkDirectory, project.ProjectCode);
			var newProjectFilename = Path.GetFileName(project.ProjectCode) + SharedConstants.FwXmlExtension;
			var newFwProjectPathname = Path.Combine(cloneLocation, newProjectFilename);

			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var helper = scope.Resolve<UpdateBranchHelperFlex>();
				if (!helper.UpdateToTheCorrectBranchHeadIfPossible(
					FdoCache.ModelVersion, actualCloneResult, cloneLocation))
				{
					actualCloneResult.Message = "Flex version is too old";
				}

				switch (actualCloneResult.FinalCloneResult)
				{
				case FinalCloneResult.ExistingCloneTargetFolder:
					Logger.Error("Clone failed: Flex project exists: {0}", cloneLocation);
					if (Directory.Exists(cloneLocation))
						Directory.Delete(cloneLocation, true);
					return false;
				case FinalCloneResult.FlexVersionIsTooOld:
					Logger.Error("Clone failed: Flex version is too old; project: {0}", project.ProjectCode);
					if (Directory.Exists(cloneLocation))
						Directory.Delete(cloneLocation, true);
					return false;
				case FinalCloneResult.Cloned:
					break;
				}

				var projectUnifier = scope.Resolve<FlexHelper>();
				var Progress = scope.Resolve<IProgress>();
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

		private static bool CheckSetup(LfMergeSettingsIni settings)
		{
			var homeFolder = Environment.GetEnvironmentVariable("HOME") ?? "/var/www";
			string[] folderPaths = new[] { Path.Combine(homeFolder, ".local"),
				Path.GetDirectoryName(settings.WebWorkDirectory) };
			foreach (string folderPath in folderPaths)
			{
				if (!Directory.Exists(folderPath))
				{
					Logger.Notice("Folder '{0}' doesn't exist", folderPath);
					return false;
				}
			}

			return true;
		}

	}
}
