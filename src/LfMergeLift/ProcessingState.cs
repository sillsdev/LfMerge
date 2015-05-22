// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMergeLift
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

		public class ProgressState
		{
			public int PercentComplete { get; set; }
			public TimeSpan ElapsedTime { get; set; }
			public TimeSpan TimeRemaining { get; set; }
			public int TotalSteps { get; set; }
			public int CurrentStep { get; set; }
			public int RetryCounter { get; set; }
		}

		public SendReceiveStates SRState { get; set; }
		public DateTime LastStateChange { get; set; }
		public ProgressState Progress { get; set; }
		public int UncommittedEditCounter { get; set; }
		public string ErrorMessage { get; set; }
		public int ErrorCode { get; set; }
	}
}

