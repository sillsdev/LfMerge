// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Logging;
using LfMerge.Settings;

namespace LfMerge.Actions
{
	public class NoneAction: Action
	{
		public NoneAction(LfMergeSettingsIni settings, ILogger logger) : base(settings, logger) {}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.IDLE; }
		}

		protected override void DoRun(ILfProject project)
		{
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

