// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Actions
{
	public class NoneAction: Action
	{
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

