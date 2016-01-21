// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

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
			UPDATING,
			HOLD,
		}

		private LfMergeSettingsIni Settings { get; set; }
		private SendReceiveStates _state;
		private long _lastStateChangeTicks;
		private int _percentComplete;
		private long _elapsedTimeSeconds;
		private long _timeRemainingSeconds;
		private int _totalSteps;
		private int _currentStep;
		private int _retryCounter;
		private int _uncommittedEditCounter;
		private string _errorMessage;
		private int _errorCode;

		protected ProcessingState()
		{
			_state = SendReceiveStates.QUEUED;
			_lastStateChangeTicks = DateTime.Now.ToUniversalTime().Ticks;
			ProjectCode = string.Empty;
		}

		public ProcessingState(string projectCode, LfMergeSettingsIni settings): this()
		{
			ProjectCode = projectCode;
			SetSettings(settings);
		}

		public void SetSettings(LfMergeSettingsIni settings)
		{
			Settings = settings;
		}

		protected virtual void SetProperty<T>(ref T property, T value)
		{
			property = value;
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
			set { SetProperty(ref _lastStateChangeTicks, value); }
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

		public string ProjectCode { get; private set; }

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
			private LfMergeSettingsIni Settings { get; set; }

			public Factory(LfMergeSettingsIni settings)
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
					ProcessingState state = JsonConvert.DeserializeObject<ProcessingState>(json);
					state.SetSettings(Settings);
					return state;
				}
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

