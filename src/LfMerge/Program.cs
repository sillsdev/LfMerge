// Copyright (c) 2011-2016 SIL International
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

namespace LfMerge
{
	public class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			MainClass.Logger.Notice("LfMerge starting with args: {0}", string.Join(" ", args));

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

				MongoConnection.Initialize();

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						RunAction(projectCode, queue.CurrentAction);

						// TODO: Verify actions complete before dequeuing
						queue.DequeueProject(projectCode);
					}
				}
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("Unhandled Exception: \n{0}", e);
				throw;
			}
			finally
			{
				if (fileLock != null)
					fileLock.ReleaseLock();

				MainClass.Container.Dispose();
				Cleanup();
			}

			MainClass.Logger.Notice("LfMerge finished");
		}

		private static void RunAction(string projectCode, IAction currentAction)
		{
			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			LanguageForgeProject project = null;
			var stopwatch = new System.Diagnostics.Stopwatch();
			try
			{
				MainClass.Logger.Notice("ProjectCode {0}", projectCode);
				project = LanguageForgeProject.Create(settings, projectCode);

				project.State.StartTimestamp = CurrentUnixTimestamp();
				stopwatch.Start();

				var ensureClone = LfMerge.Core.Actions.Action.GetAction(ActionNames.EnsureClone);
				ensureClone.Run(project);

				if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
					currentAction.Run(project);
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("Putting project {0} on hold due to unhandled exception: \n{1}", projectCode, e);
				if (project != null)
					project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
			}
			finally
			{
				stopwatch.Stop();
				if (project != null && project.State != null)
					project.State.PreviousRunTotalMilliseconds = stopwatch.ElapsedMilliseconds;
				if (project != null && project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;

				// Dispose FDO cache to free memory
				LanguageForgeProject.DisposeFwProject(project);
			}
		}

		/// <summary>
		/// Clean up anything needed before quitting, e.g. disposing of IDisposable objects.
		/// </summary>
		private static void Cleanup()
		{
			LanguageForgeProject.DisposeProjectCache();
		}

		private static bool CheckSetup(LfMergeSettings settings)
		{
			var homeFolder = Environment.GetEnvironmentVariable("HOME") ?? "/var/www";
			string[] folderPaths = new[] { Path.Combine(homeFolder, ".local"),
				Path.GetDirectoryName(settings.WebWorkDirectory) };
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

		private static DateTime _unixEpoch = new DateTime(1970, 1, 1);

		private static long CurrentUnixTimestamp()
		{
			// http://stackoverflow.com/a/9453127/2314532
			TimeSpan sinceEpoch = DateTime.UtcNow - _unixEpoch;
			return (long)sinceEpoch.TotalSeconds;
		}
	}
}
