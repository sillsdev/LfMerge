// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Newtonsoft.Json;

namespace LfMerge
{
	public class ProcessingState
	{
		public enum SendReceiveStates
		{
			QUEUED,
			IDLE,
			MERGING,
			SENDING,
			RECEIVING,
			HOLD,
		}

		protected ProcessingState()
		{
			SRState = SendReceiveStates.QUEUED;
			LastStateChangeTicks = DateTime.Now.ToUniversalTime().Ticks;
			ProjectCode = string.Empty;
		}

		public ProcessingState(string projectCode): this()
		{
			ProjectCode = projectCode;
		}

		public SendReceiveStates SRState { get; set; }
		public long LastStateChangeTicks { get; set; }
		public int PercentComplete { get; set; }
		public long ElapsedTimeSeconds { get; set; }
		public long TimeRemainingSeconds { get; set; }
		public int TotalSteps { get; set; }
		public int CurrentStep { get; set; }
		public int RetryCounter { get; set; }
		public int UncommittedEditCounter { get; set; }
		public string ErrorMessage { get; set; }
		public int ErrorCode { get; set; }

		public string ProjectCode { get; set; }

		public override bool Equals(object obj)
		{
			var other = obj as ProcessingState;
			if (other == null)
				return false;
			return SRState == other.SRState && LastStateChangeTicks == other.LastStateChangeTicks &&
				PercentComplete == other.PercentComplete &&
				ElapsedTimeSeconds == other.ElapsedTimeSeconds &&
				TimeRemainingSeconds == other.TimeRemainingSeconds &&
				TotalSteps == other.TotalSteps &&
				CurrentStep == other.CurrentStep &&
				RetryCounter == other.RetryCounter &&
				UncommittedEditCounter == other.UncommittedEditCounter &&
				ErrorMessage == other.ErrorMessage && ErrorCode == other.ErrorCode &&
				ProjectCode == other.ProjectCode;
		}

		public override int GetHashCode()
		{
			var hash = SRState.GetHashCode() ^ LastStateChangeTicks.GetHashCode() ^
				PercentComplete ^ ElapsedTimeSeconds.GetHashCode() ^ TimeRemainingSeconds.GetHashCode() ^
				TotalSteps ^ CurrentStep ^ RetryCounter ^ UncommittedEditCounter ^ ErrorCode ^
				ProjectCode.GetHashCode();
			if (ErrorMessage != null)
				hash ^= ErrorMessage.GetHashCode();
			return hash;
		}

		public static void Serialize(ProcessingState state)
		{
			var json = JsonConvert.SerializeObject(state);

			var fileName = LfMergeDirectories.Current.GetStateFileName(state.ProjectCode);
			File.WriteAllText(fileName, json);
		}

		public static ProcessingState Deserialize(string projectCode)
		{
			var fileName = LfMergeDirectories.Current.GetStateFileName(projectCode);
			if (File.Exists(fileName))
			{
				var json = File.ReadAllText(fileName);
				return JsonConvert.DeserializeObject<ProcessingState>(json);
			}
			return new ProcessingState(projectCode);
		}
	}
}

