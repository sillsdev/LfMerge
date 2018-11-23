// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using LfMerge.Core.Tools;
using SIL.IO.FileLock;
using SIL.LCModel;
using SIL.WritingSystems;

namespace LfMerge.QueueManager
{
	public static class QueueManager
	{
		[STAThread]
		public static void Main(string[] args)
		{
			ExceptionLogging.Initialize("17a42e4a67dd2e42d4aa40d8bf2d23ee", Assembly.GetExecutingAssembly().GetName().Name);
			var options = QueueManagerOptions.ParseCommandLineArgs(args);
			if (options == null)
				return;

			MainClass.Logger.Notice("LfMergeQueueManager starting with args: {0}", string.Join(" ", args));

			// initialize the SLDR
			Sldr.Initialize();

			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			var fileLock = SimpleFileLock.CreateFromFilePath(settings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					MainClass.Logger.Error("Can't acquire file lock - is another instance running?");
					return;
				}
				MainClass.Logger.Notice("Lock acquired");

				if (!CheckSetup(settings))
					return;

				// Cleanup any hang projects
				new Janitor(settings, MainClass.Logger).CleanupAndRescheduleJobs();

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						var projectPath = Path.Combine(settings.LcmDirectorySettings.ProjectsDirectory,
							projectCode, $"{projectCode}{LcmFileHelper.ksFwDataXmlFileExtension}");
						var modelVersion = FwProject.GetModelVersion(projectPath);
						queue.DequeueProject(projectCode);
						int retCode = MainClass.StartLfMerge(projectCode, queue.CurrentActionName,
							modelVersion, true);

						// TODO: If LfMerge fails, should we re-queue the project, or not?
						if (retCode != 0)
						{
							// queue.EnqueueProject(projectCode);
						}
					}
				}
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("Unhandled Exception:\n{0}", e);
				throw;
			}
			finally
			{
				if (fileLock != null)
					fileLock.ReleaseLock();

				if (Sldr.IsInitialized)
					Sldr.Cleanup();

				MainClass.Container.Dispose();
			}

			MainClass.Logger.Notice("LfMergeQueueManager finished");
		}

		private static bool CheckSetup(LfMergeSettings settings)
		{
			var homeFolder = Environment.GetEnvironmentVariable("HOME") ?? "/var/www";
			string[] folderPaths = { Path.Combine(homeFolder, ".local"),
				Path.GetDirectoryName(settings.WebWorkDirectory)
			};
			foreach (string folderPath in folderPaths)
			{
				if (!Directory.Exists(folderPath))
				{
					MainClass.Logger.Notice("Folder '{0}' doesn't exist", folderPath);
					return false;
				}
			}

			return true;
		}
	}
}
