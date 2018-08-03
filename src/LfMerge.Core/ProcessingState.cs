// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Core.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LfMerge.Core
{
	public class ProcessingState
	{
		/// <summary>
		/// LfMerge project send/receive state as defined in "Language Forge Send/Receive technical design and scenarios"
		/// </summary>
		public enum SendReceiveStates
		{
			/// <summary>
			/// Initial clone for the LF project is being performed.
			/// </summary>
			CLONING,

			/// <summary>
			/// The project has just been successfully cloned, so no further action is necessary during this run of LfMerge
			/// </summary>
			CLONED,

			/// <summary>
			/// Synchronize action is being performed
			/// </summary>
			SYNCING,

			/// <summary>
			/// LfMerge is idle for the current project. No errors
			/// </summary>
			IDLE,

			/// <summary>
			/// A project in this state is skipped from processing due to previous failed merge
			/// </summary>
			HOLD,

			/// <summary>
			/// A recoverable error occured
			/// </summary>
			ERROR
		}

		/// <summary>
		/// Error codes.
		/// </summary>
		public enum ErrorCodes
		{
			NoError = 0,
			Unspecified = 1,

			EmptyProject = 10,
			NoFlexProject = 11,

			UnhandledException = 20,
			Unauthorized       = 30,

			UnspecifiedBranchError = 50,
			ProjectTooOld = 51, // Project < 7000068
			ProjectTooNew = 52
		}

		private LfMergeSettings Settings { get; set; }
		private SendReceiveStates _state;
		private long _lastStateChangeTicks;
		private long _startTimestamp; // Should be seconds since Unix epoch (1970-01-01 at 00:00:00 UTC)
		private int _percentComplete;
		private long _elapsedTimeSeconds;
		private long _timeRemainingSeconds;
		private int _totalSteps;
		private int _currentStep;
		private int _retryCounter;
		private int _uncommittedEditCounter;
		private string _errorMessage;
		private int _errorCode;
		private long _previousRunTotalMilliseconds;

		protected ProcessingState()
		{
			_state = SendReceiveStates.CLONING;
			_lastStateChangeTicks = DateTime.UtcNow.Ticks;
			ProjectCode = string.Empty;
		}

		public ProcessingState(string projectCode, LfMergeSettings settings): this()
		{
			ProjectCode = projectCode;
			SetSettings(settings);
		}

		public void SetSettings(LfMergeSettings settings)
		{
			Settings = settings;
		}

		public void PutOnHold(string errorMessage, params object[] args)
		{
			if (errorMessage == null)
				// Fallback. ALWAYS provide an error message when you call this function, otherwise
				// the user will see this VERY uninformative error message. Don't let that happen.
				errorMessage = "Project going on hold due to unspecified error";
			SetErrorState(SendReceiveStates.HOLD, ErrorCodes.Unspecified, errorMessage, args);
		}

		public void SetErrorState(SendReceiveStates state, ErrorCodes errorCode, string errorMessage, params object[] args)
		{
			SetErrorState(state, (int)errorCode, errorMessage, args);
		}

		public void SetErrorState(SendReceiveStates state, int errorCode, string errorMessage, params object[] args)
		{
			if (errorMessage == null)
				throw new ArgumentNullException("errorMessage");
			_errorMessage = string.Format(errorMessage, args); // Avoid setting this one via property so that we don't update state file yet
			_errorCode = errorCode;
			SRState = state; // But set this one via property so that state file is updated, just once.
		}

		protected virtual void SetProperty<T>(ref T property, T value)
		{
			property = value;
			_lastStateChangeTicks = DateTime.UtcNow.Ticks;
			Serialize();
		}

		public SendReceiveStates SRState
		{
			get { return _state; }
			set { SetProperty(ref _state, value); }
		}
		public long LastStateChangeTicks
		{
			get { return _lastStateChangeTicks; }
			// Avoid calling SetProperty() for this one so that we can set an "old" value if desired.
			set { _lastStateChangeTicks = value; Serialize(); }
		}
		public long StartTimestamp
		{
			get { return _startTimestamp; }
			set { SetProperty(ref _startTimestamp, value); }
		}
		public int PercentComplete
		{
			get { return _percentComplete; }
			set { SetProperty(ref _percentComplete, value); }
		}
		public long ElapsedTimeSeconds
		{
			get { return _elapsedTimeSeconds; }
			set { SetProperty(ref _elapsedTimeSeconds, value); }
		}
		public long TimeRemainingSeconds
		{
			get { return _timeRemainingSeconds; }
			set { SetProperty(ref _timeRemainingSeconds, value); }
		}
		public int TotalSteps
		{
			get { return _totalSteps; }
			set { SetProperty(ref _totalSteps, value); }
		}
		public int CurrentStep
		{
			get { return _currentStep; }
			set { SetProperty(ref _currentStep, value); }
		}
		public int RetryCounter
		{
			get { return _retryCounter; }
			set { SetProperty(ref _retryCounter, value); }
		}
		public int UncommittedEditCounter
		{
			get { return _uncommittedEditCounter; }
			set { SetProperty(ref _uncommittedEditCounter, value); }
		}
		public string ErrorMessage
		{
			get { return _errorMessage; }
			set { SetProperty(ref _errorMessage, value); }
		}
		public int ErrorCode
		{
			get { return _errorCode; }
			set { SetProperty(ref _errorCode, value); }
		}
		public long PreviousRunTotalMilliseconds
		{
			get { return _previousRunTotalMilliseconds; }
			set { SetProperty(ref _previousRunTotalMilliseconds, value); }
		}

		public string ProjectCode { get; private set; }

		public override bool Equals(object obj)
		{
			var other = obj as ProcessingState;
			if (other == null)
				return false;
			return SRState == other.SRState &&
				// LastStateChangeTicks == other.LastStateChangeTicks &&  // NOPE. Not now that this changes all the time.
				StartTimestamp == other.StartTimestamp &&
				PercentComplete == other.PercentComplete &&
				ElapsedTimeSeconds == other.ElapsedTimeSeconds &&
				TimeRemainingSeconds == other.TimeRemainingSeconds &&
				TotalSteps == other.TotalSteps &&
				CurrentStep == other.CurrentStep &&
				RetryCounter == other.RetryCounter &&
				UncommittedEditCounter == other.UncommittedEditCounter &&
				ErrorMessage == other.ErrorMessage && ErrorCode == other.ErrorCode &&
				PreviousRunTotalMilliseconds == other.PreviousRunTotalMilliseconds &&
				ProjectCode == other.ProjectCode;
		}

		public override int GetHashCode()
		{
			var hash = SRState.GetHashCode() ^
				// LastStateChangeTicks.GetHashCode() ^  // NOPE. This changes too often.
				StartTimestamp.GetHashCode() ^
				PercentComplete ^ ElapsedTimeSeconds.GetHashCode() ^ TimeRemainingSeconds.GetHashCode() ^
				TotalSteps ^ CurrentStep ^ RetryCounter ^ UncommittedEditCounter ^ ErrorCode ^
				PreviousRunTotalMilliseconds.GetHashCode() ^ ProjectCode.GetHashCode();
			if (ErrorMessage != null)
				hash ^= ErrorMessage.GetHashCode();
			return hash;
		}

		public void Serialize()
		{
			if (Settings == null)
				// Don't serialize while we're still being deserialized!
				return;

			JsonConvert.DefaultSettings = (() => {
				var settings = new JsonSerializerSettings();
				settings.Converters.Add(new StringEnumConverter());
				return settings;
			});

			var json = JsonConvert.SerializeObject(this, Formatting.Indented);

			var fileName = Settings.GetStateFileName(ProjectCode);
			File.WriteAllText(fileName, json);
		}

		public class Factory: IProcessingStateDeserialize
		{
			private LfMergeSettings Settings { get; set; }

			public Factory(LfMergeSettings settings)
			{
				Settings = settings;
			}

			public ProcessingState Deserialize(string projectCode)
			{
				var fileName = Settings.GetStateFileName(projectCode);
				if (File.Exists(fileName))
				{
					var json = File.ReadAllText(fileName);
					// TODO: Use http://stackoverflow.com/a/8312048/2314532 instead of this hack
					try
					{
						ProcessingState state = JsonConvert.DeserializeObject<ProcessingState>(json);
						if (state != null)
						{
							state.SetSettings(Settings);
							return state;
						}
					}
					catch (JsonReaderException)
					{
					}
				}
				// If the state file is nonexistent or invalid Json, set the project back to CLONING
				if (!File.Exists(fileName))
					MainClass.Logger.Notice("State file doesn't exist, so setting the project back to CLONING");
				else
					MainClass.Logger.Notice("State file was invalid Json, so setting the project back to CLONING");
				return new ProcessingState(projectCode, Settings);
			}
		}

		public static ProcessingState Deserialize(string projectCode)
		{
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var factory = scope.Resolve<IProcessingStateDeserialize>();
				return factory.Deserialize(projectCode);
			}
		}
	}
}

