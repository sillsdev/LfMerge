// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using LfMerge.Core.Logging;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;

namespace LfMerge.Core.Tools
{
	public class Janitor
	{
		private LfMergeSettings Settings { get; set; }
		private ILogger Logger { get; set; }

		public Janitor(LfMergeSettings settings, ILogger logger)
		{
			Settings = settings;
			Logger = logger;
		}

		public void CleanupAndRescheduleJobs()
		{
			foreach (var file in Directory.EnumerateFiles(Settings.StateDirectory))
			{
				var projectCode = Path.GetFileNameWithoutExtension(file);
				var state = ProcessingState.Deserialize(projectCode);
				switch (state.SRState)
				{
					case ProcessingState.SendReceiveStates.CLONED:
					case ProcessingState.SendReceiveStates.IDLE:
					case ProcessingState.SendReceiveStates.HOLD:
					case ProcessingState.SendReceiveStates.ERROR:
						// Nothing to do
						break;
					case ProcessingState.SendReceiveStates.CLONING:
					case ProcessingState.SendReceiveStates.SYNCING:
						RescheduleProject(projectCode, state,
							string.Format("QueueManager detected project '{0}' in unclean state '{1}'; rescheduled",
							projectCode, state.SRState));
						break;
					default:
						RescheduleProject(projectCode, state,
							string.Format("QueueManager detected unknown state '{0}' for project '{1}'; rescheduled",
								state.SRState, projectCode));
						break;
				}
			}
		}

		private void RescheduleProject(string projectCode, ProcessingState state, string message)
		{
			Logger.Error(message);
			ExceptionLogging.Client.Notify(new ProjectInUncleanStateException(message));
			state.SRState = ProcessingState.SendReceiveStates.IDLE;
			state.Serialize();
			Queue.GetQueue(QueueNames.Synchronize).EnqueueProject(projectCode);
		}
	}
}