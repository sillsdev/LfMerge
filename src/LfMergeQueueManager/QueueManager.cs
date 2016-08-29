// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.Actions;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using Palaso.IO.FileLock;
using System.Diagnostics;

namespace LfMerge.QueueManager
{
	public static class QueueManager
	{
		[STAThread]
		public static void Main(string[] args)
		{
			var options = QueueManagerOptions.ParseCommandLineArgs(args);
			if (options == null)
				return;

			MainClass.Logger.Notice("LfMergeQueueManager starting with args: {0}", string.Join(" ", args));

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

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						RunAction(projectCode, queue.CurrentActionName);

						// TODO: Verify actions complete before dequeuing
						queue.DequeueProject(projectCode);
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

				MainClass.Container.Dispose();
			}

			MainClass.Logger.Notice("LfMergeQueueManager finished");
		}

		private static void RunAction(string projectCode, ActionNames currentAction)
		{
			var startInfo = new ProcessStartInfo("LfMerge.exe");
			startInfo.Arguments = string.Format("--clone -p {0} --action {1}", projectCode, currentAction);
			startInfo.CreateNoWindow = true;
			startInfo.ErrorDialog = false;
			startInfo.UseShellExecute = false;
			try
			{
				using (var process = Process.Start(startInfo))
				{
					process.WaitForExit();
				}
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("LfMergeQueueManager: Unhandled exception trying to start {0} {1}\n{2}",
					startInfo.FileName, startInfo.Arguments, e);
			}
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
