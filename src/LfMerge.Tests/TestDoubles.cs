// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.IO;
using Chorus.Model;
using IniParser.Model;
using LfMerge.FieldWorks;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.Progress;

namespace LfMerge.Tests
{
	public class ProcessingStateFactoryDouble: IProcessingStateDeserialize
	{
		public ProcessingStateDouble State { get; set; }
		private LfMergeSettingsIni Settings { get; set; }

		public ProcessingStateFactoryDouble(LfMergeSettingsIni settings)
		{
			Settings = settings;
		}

		#region IProcessingStateDeserialize implementation
		public ProcessingState Deserialize(string projectCode)
		{
			if (State == null)
				State = new ProcessingStateDouble(projectCode, Settings);
			return State;
		}
		#endregion
	}

	public class ProcessingStateDouble: ProcessingState
	{
		public List<ProcessingState.SendReceiveStates> SavedStates;

		public ProcessingStateDouble(string projectCode, LfMergeSettingsIni settings): base(projectCode, settings)
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
		protected LanguageForgeProjectAccessor(LfMergeSettingsIni settings): base(settings, null)
		{
		}

		public static void Reset()
		{
			CachedProjects.Clear();
		}
	}

	public class LfMergeSettingsDouble: LfMergeSettingsIni
	{
		public LfMergeSettingsDouble(string replacementBaseDir) : base()
		{
			var replacementConfig = new IniData(ParsedConfig);
			replacementConfig.Global["BaseDir"] = replacementBaseDir;
			Initialize(replacementConfig);
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

	class FlexHelperDouble: FlexHelper
	{
		public override void PutHumptyTogetherAgain(IProgress progress, bool verbose, string mainFilePathname)
		{
		}
	}

	class MongoProjectRecordFactoryDouble: MongoProjectRecordFactory
	{
		public MongoProjectRecordFactoryDouble(IMongoConnection connection): base(connection)
		{
		}

		public override MongoProjectRecord Create(ILfProject project)
		{
			return null;
		}
	}
}
