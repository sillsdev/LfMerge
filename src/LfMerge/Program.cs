// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Actions;
using LfMerge.Actions.Infrastructure;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using LfMerge.Queues;
using LfMerge.Settings;
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
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<ChorusHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<MongoConnection>().SingleInstance().As<IMongoConnection>().ExternallyOwned();
			containerBuilder.RegisterType<MongoProjectRecordFactory>().AsSelf();
			containerBuilder.RegisterType<SyslogProgress>().As<IProgress>();
			Actions.Action.Register(containerBuilder);
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

// REVIEW Eberhard(RandyR): This method sure feels like it should be in a new LF 'Action' subclass.
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
			using (var scope = Container.BeginLifetimeScope())
			{
				var progress = scope.Resolve<IProgress>();
				var settings = Container.Resolve<LfMergeSettingsIni>();

				try
				{
					// Check if an initial clone needs to be performed
					if (File.Exists(settings.GetStateFileName(project.ProjectCode)) && (project.State.SRState != ProcessingState.SendReceiveStates.CLONING))
					{
						return;
					}
					Logger.Notice("Initial clone");
					// Since we're in here, the previous clone was not finished, so remove and start over
					var cloneLocation = Path.Combine(settings.WebWorkDirectory, project.ProjectCode);
					if (LanguageForgeBridgeServices.CanDeleteCloneFolderCandidate(cloneLocation))
					{
						Logger.Notice("Cleaning out previous failed clone at {0}", cloneLocation);
						Directory.Delete(cloneLocation, true);
					}
					project.State.SRState = ProcessingState.SendReceiveStates.CLONING;

					var projectFolderPath = Path.Combine(settings.WebWorkDirectory, project.ProjectCode);
					var chorusHelper = Container.Resolve<ChorusHelper>();
					string cloneResult;
					var options = new Dictionary<string, string>
					{
						{"projectPath", projectFolderPath},
						{"fdoDataModelVersion", FdoCache.ModelVersion},
						{"languageDepotRepoUri", chorusHelper.GetSyncUri(project)}
					};
					LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Clone", progress, options, out cloneResult);

					var line = LanguageForgeBridgeServices.GetLineStartingWith(cloneResult, "Clone created in folder");
					if (!string.IsNullOrEmpty(line))
					{
						// Dig out actual clone path from 'line'.
						var actualClonePath = line.Replace("Clone created in folder ", string.Empty).Split(',')[0].Trim();
						if (projectFolderPath != actualClonePath)
							Logger.Notice("Warning: Folder {0} already exists, so project cloned in {1}", projectFolderPath, actualClonePath);
					}
					line = LanguageForgeBridgeServices.GetLineStartingWith(cloneResult, "Specified branch did not exist");
					if (!string.IsNullOrEmpty(line))
					{
						// Updated to a branch, but an earlier one than is in "FdoCache.ModelVersion".
						// That is fine, since FDO will update (e.g., do a data migration on) that older version to the one in FdoCache.ModelVersion.
						// Then, the next commit. or full S/R operation will create the new branch at FdoCache.ModelVersion.
						// I (RandyR) suspect this to not happen any time soon, if LF starts with DM '68'.
						// So, this is essentially future-proofing LF Merge for some unknown day in the future, when this coud happen.
						Logger.Notice(line);
					}

					Logger.Notice("Initial transfer to mongo after clone");
					project.IsInitialClone = true;
					Actions.Action.GetAction(ActionNames.TransferFdoToMongo).Run(project);
					project.IsInitialClone = false;
				}
				catch (Exception e)
				{
					if (e.GetType().Name == "ArgumentOutOfRangeException" && e.Message == "Cannot update to any branch.")
					{
						project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
						throw;
					}
					if (e.GetType().Name == "RepositoryAuthorizationException")
					{
						Logger.Error("Initial clone authorization exception");
						project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
						throw;
					}
				}
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
