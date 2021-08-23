// Copyright (c) 2011-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using Bugsnag;

namespace LfMerge
{
	public static class Program
	{
		[STAThread]
		public static int Main(string[] args)
		{
			ExceptionLogging.Initialize("17a42e4a67dd2e42d4aa40d8bf2d23ee", Assembly.GetExecutingAssembly().GetName().Name);
			int result = (int)ErrorCode.NoError;
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return (int)ErrorCode.InvalidOptions;

			// Username and Password will usually be "x" because it's dealt with on Language Forge site.
			// However, when debugging LfMerge we want to be able to set it to a real name
			ChorusHelper.Username = options.User;
			ChorusHelper.Password = System.Environment.GetEnvironmentVariable("LD_TRUST_TOKEN") ?? options.Password;

			ExceptionLogging.Client.AddInfo(options.ProjectCode, MainClass.ModelVersion);

			MainClass.Logger.Notice("LfMerge {2} (database {0}) starting with args: {1}",
				MainClass.ModelVersion, string.Join(" ", args), MainClass.GetVersionInfo("SemVer"));

			if (string.IsNullOrEmpty(options.ProjectCode))
			{
				MainClass.Logger.Error("Command line doesn't contain project code - exiting.");
				return -1;
			}

			if (!string.IsNullOrEmpty(options.ConfigDir))
				LfMergeSettings.ConfigDir = options.ConfigDir;

			FwProject.AllowDataMigration = options.AllowDataMigration;

			string differentModelVersion = null;
			try
			{
				if (!MainClass.CheckSetup())
					return (int)ErrorCode.GeneralError;

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
				result = MainClass.StartLfMerge(options.ProjectCode, options.CurrentAction,
					differentModelVersion, false, options.ConfigDir);
			}

			MainClass.Logger.Notice("LfMerge-{0} finished", MainClass.ModelVersion);
			return result;
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
					project.State.SRState != ProcessingState.SendReceiveStates.ERROR &&
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
				MainClass.Logger.Error("Got exception {0}", e.ToString());
				ExceptionLogging.Client.Notify(e);
				if (projectCode == null)
				{
					MainClass.Logger.Error("Project code was null");
				}

				if (project.State.SRState != ProcessingState.SendReceiveStates.ERROR)
				{
					MainClass.Logger.Error(string.Format(
						"Putting project '{0}' on hold due to unhandled exception: \n{1}",
						projectCode, e));
					if (project != null)
					{
						project.State.SetErrorState(ProcessingState.SendReceiveStates.HOLD,
							ProcessingState.ErrorCodes.UnhandledException, string.Format(
								"Putting project '{0}' on hold due to unhandled exception: \n{1}",
								projectCode, e));
					}
				}
			}
			finally
			{
				stopwatch.Stop();
				if (project != null && project.State != null)
					project.State.PreviousRunTotalMilliseconds = stopwatch.ElapsedMilliseconds;
				if (project != null && project.State.SRState != ProcessingState.SendReceiveStates.HOLD &&
					project.State.SRState != ProcessingState.SendReceiveStates.ERROR &&
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
