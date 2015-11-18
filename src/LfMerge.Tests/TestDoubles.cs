// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using Chorus.Model;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibFLExBridgeChorusPlugin;
using SIL.Progress;
using LibTriboroughBridgeChorusPlugin.Infrastructure;

namespace LfMerge.Tests
{
	public class ProcessingStateFactoryDouble: IProcessingStateDeserialize
	{
		public ProcessingStateDouble State { get; set; }

		#region IProcessingStateDeserialize implementation
		public ProcessingState Deserialize(string projectCode)
		{
			if (State == null)
				State = new ProcessingStateDouble(projectCode);
			return State;
		}
		#endregion
	}

	public class ProcessingStateDouble: ProcessingState
	{
		public List<ProcessingState.SendReceiveStates> SavedStates;

		public ProcessingStateDouble(string projectCode): base(projectCode)
		{
			SavedStates = new List<ProcessingState.SendReceiveStates>();
		}

		protected override void SetProperty<T>(ref T property, T value)
		{
			property = value;

			if (SavedStates.Count == 0 || SavedStates[SavedStates.Count - 1] != SRState)
				SavedStates.Add(SRState);
		}

		public void ResetSavedStates()
		{
			SavedStates.Clear();
		}
	}

	public class LanguageForgeProjectAccessor: LanguageForgeProject
	{
		protected LanguageForgeProjectAccessor(): base(null)
		{
		}

		public static void Reset()
		{
			CachedProjects.Clear();
		}
	}

	public class LfMergeSettingsAccessor: LfMergeSettings
	{
		public static void ResetCurrent()
		{
			Current = null;
		}
	}

	class LanguageDepotProjectDouble: ILanguageDepotProject
	{
		#region ILanguageDepotProject implementation
		public void Initialize(string lfProjectCode)
		{
			ProjectCode = lfProjectCode;
		}

		public string Username { get; set; }
		public string Password { get; set; }
		public string ProjectCode { get; set; }
		#endregion
	}

	class InternetCloneSettingsModelDouble: InternetCloneSettingsModel
	{
		public override void DoClone()
		{
			Directory.CreateDirectory(TargetDestination);
			Directory.CreateDirectory(Path.Combine(TargetDestination, ".hg"));
			File.WriteAllText(Path.Combine(TargetDestination, ".hg", "hgrc"), "blablabla");
		}
	}

	class UpdateBranchHelperFlexDouble: UpdateBranchHelperFlex
	{
		public override bool UpdateToTheCorrectBranchHeadIfPossible(string desiredBranchName,
			ActualCloneResult cloneResult, string cloneLocation)
		{
			cloneResult.FinalCloneResult = FinalCloneResult.Cloned;
			return true;
		}
	}

	class ProjectUnifierDouble: IProjectUnifier
	{
		#region IProjectUnifier implementation
		public void PutHumptyTogetherAgain(IProgress progress, bool writeVerbose, string mainFilePathname)
		{
		}
		#endregion
	}
}

