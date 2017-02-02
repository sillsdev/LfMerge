// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;

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

			MainClass.Logger.Notice("LfMerge (database {0}) starting with args: {1}",
				MainClass.ModelVersion, string.Join(" ", args));

			if (!string.IsNullOrEmpty(options.ConfigDir))
				LfMergeSettings.ConfigDir = options.ConfigDir;

			FwProject.AllowDataMigration = options.AllowDataMigration;

			string differentModelVersion = null;
			try
			{
				if (!MainClass.CheckSetup())
					return;

				MongoConnection.Initialize();

				differentModelVersion = RunAction(options.ProjectCode, options.CurrentAction);
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("Unhandled Exception: \n{0}", e);
				throw;
			}
			finally
			{
				MainClass.Container.Dispose();
				Cleanup();
			}

			if (!string.IsNullOrEmpty(differentModelVersion))
			{
				MainClass.StartLfMerge(options.ProjectCode, options.CurrentAction,
					differentModelVersion, false, options.ConfigDir);
			}

			MainClass.Logger.Notice("LfMerge-{0} finished", MainClass.ModelVersion);
		}

		private static string RunAction(string projectCode, ActionNames currentAction)
		{
			LanguageForgeProject project = null;
			var stopwatch = new Stopwatch();
			try
			{
				MainClass.Logger.Notice("ProjectCode {0}", projectCode);
				project = LanguageForgeProject.Create(projectCode);

				project.State.StartTimestamp = CurrentUnixTimestamp();
				stopwatch.Start();

				if (Options.Current.CloneProject)
				{
					var cloneLocation = project.ProjectDir;
					if (Directory.Exists(cloneLocation) && !File.Exists(project.FwDataPath))
					{
						// If we a .hg directory but no project file it means the previous clone
						// was not finished, so remove and start over
						MainClass.Logger.Notice("Cleaning out previous failed clone at {0}", cloneLocation);
						Directory.Delete(cloneLocation, true);
						project.State.SRState = ProcessingState.SendReceiveStates.CLONING;
					}
				}

				var ensureClone = LfMerge.Core.Actions.Action.GetAction(ActionNames.EnsureClone);
				ensureClone.Run(project);

				if (ChorusHelper.RemoteDataIsForDifferentModelVersion)
				{
					// The repo is for an older model version
					var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
					return chorusHelper.ModelVersion;
				}

				if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD &&
				    project.State.SRState != ProcessingState.SendReceiveStates.CLONED)
				{
					LfMerge.Core.Actions.Action.GetAction(currentAction).Run(project);

					if (ChorusHelper.RemoteDataIsForDifferentModelVersion)
					{
						// The repo is for an older model version
						var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
						return chorusHelper.ModelVersion;
					}
				}
			}
			catch (Exception e)
			{
				string errorMsg = string.Format("Putting project {0} on hold due to unhandled exception: \n{1}",
					projectCode, e);
				MainClass.Logger.Error(errorMsg);
				if (project != null)
					project.State.PutOnHold(errorMsg);
			}
			finally
			{
				stopwatch.Stop();
				if (project != null && project.State != null)
					project.State.PreviousRunTotalMilliseconds = stopwatch.ElapsedMilliseconds;
				if (project != null && project.State.SRState != ProcessingState.SendReceiveStates.HOLD &&
					!ChorusHelper.RemoteDataIsForDifferentModelVersion)
				{
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;
				}

				// Dispose FDO cache to free memory
				LanguageForgeProject.DisposeFwProject(project);
			}
			return null;
		}

		/// <summary>
		/// Clean up anything needed before quitting, e.g. disposing of IDisposable objects.
		/// </summary>
		private static void Cleanup()
		{
			LanguageForgeProject.DisposeProjectCache();
		}

		private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1);

		private static long CurrentUnixTimestamp()
		{
			// http://stackoverflow.com/a/9453127/2314532
			TimeSpan sinceEpoch = DateTime.UtcNow - _unixEpoch;
			return (long)sinceEpoch.TotalSeconds;
		}
	}
}
