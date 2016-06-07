// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using Autofac;
using LfMerge.Actions.Infrastructure;
using LfMerge.Logging;
using LfMerge.Settings;
using SIL.FieldWorks.FDO;

namespace LfMerge.Actions
{
	/// <summary>
	/// Ensure that we have a clone of the repo.
	/// </summary>
	/// <remarks>This is a special action: it doesn't have a corresponding queue and will always
	/// be called. It will only do something if we don't have a clone yet.</remarks>
	public class EnsureCloneAction: Action
	{
		private ILfProject _currentProject;

		public EnsureCloneAction(LfMergeSettingsIni settings, ILogger logger): base(settings, logger)
		{
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return _currentProject.State.SRState; }
		}

		public override void PreRun(ILfProject project)
		{
			_currentProject = project;
			if (!File.Exists(Settings.GetStateFileName(_currentProject.ProjectCode)))
				_currentProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
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
		protected override void DoRun(ILfProject project)
		{
			try
			{
				// Check if an initial clone needs to be performed
				if (File.Exists(Settings.GetStateFileName(project.ProjectCode)) &&
					(project.State.SRState != ProcessingState.SendReceiveStates.CLONING))
				{
					return;
				}
				Logger.Notice("Initial clone");
				// Since we're in here, the previous clone was not finished, so remove and start over
				var cloneLocation = Path.Combine(Settings.WebWorkDirectory, project.ProjectCode);
				if (Directory.Exists(cloneLocation))
				{
					Logger.Notice("Cleaning out previous failed clone at {0}", cloneLocation);
					Directory.Delete(cloneLocation, true);
				}
				project.State.SRState = ProcessingState.SendReceiveStates.CLONING;

				string cloneResult = CloneRepo(project, cloneLocation);

				var line = LfMergeBridgeServices.GetLineStartingWith(cloneResult,
					"Clone created in folder");
				if (!string.IsNullOrEmpty(line))
				{
					// Dig out actual clone path from 'line'.
					var actualClonePath = line.Replace("Clone created in folder ",
						string.Empty).Split(',')[0].Trim();
					if (cloneLocation != actualClonePath)
					{
						Logger.Notice("Warning: Folder {0} already exists, so project cloned in {1}",
							cloneLocation, actualClonePath);
					}
				}
				line = LfMergeBridgeServices.GetLineStartingWith(cloneResult,
					"Specified branch did not exist");
				if (!string.IsNullOrEmpty(line))
				{
					// Updated to a branch, but an earlier one than is in "FdoCache.ModelVersion".
					// That is fine, since FDO will update (e.g., do a data migration on) that
					// older version to the one in FdoCache.ModelVersion.
					// Then, the next commit. or full S/R operation will create the new branch at
					// FdoCache.ModelVersion. I (RandyR) suspect this to not happen any time soon,
					// if LF starts with DM '68'. So, this is essentially future-proofing LF Merge
					// for some unknown day in the future, when this coud happen.
					Logger.Notice(line);
				}

				Logger.Notice("Initial transfer to mongo after clone");
				project.IsInitialClone = true;
				Actions.Action.GetAction(ActionNames.TransferFdoToMongo).Run(project);
				project.IsInitialClone = false;
			}
			catch (Exception e)
			{
				if (e.GetType().Name == "ArgumentOutOfRangeException" &&
					e.Message == "Cannot update to any branch.")
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

		protected virtual string CloneRepo(ILfProject project, string projectFolderPath)
		{
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			string cloneResult;
			var options = new Dictionary<string, string> {
				{ "projectPath", projectFolderPath },
				{ "fdoDataModelVersion", FdoCache.ModelVersion },
				{ "languageDepotRepoUri", chorusHelper.GetSyncUri(project) }
			};
			LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Clone", Progress, options, out cloneResult);
			return cloneResult;
		}

	}
}

