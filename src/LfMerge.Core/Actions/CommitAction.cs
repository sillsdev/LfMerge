// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;

namespace LfMerge.Core.Actions
{
	public class CommitAction: Action
	{
		public CommitAction(LfMergeSettings settings, ILogger logger) : base(settings, logger) {}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		protected override void DoRun(ILfProject project)
		{
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.Synchronize; }
		}
	}
}

