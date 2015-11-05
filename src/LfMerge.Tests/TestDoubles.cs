// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;

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

}

