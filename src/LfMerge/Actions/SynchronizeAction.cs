// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.Model;
using LfMerge.FieldWorks;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.Progress;
using SIL.Reporting;

namespace LfMerge.Actions
{
	public class SynchronizeAction: Action
	{
		public SynchronizeAction(LfMergeSettingsIni settings, LfMerge.Logging.ILogger logger) : base(settings, logger) {}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.RECEIVING; }
		}

		protected override void DoRun(ILfProject project)
		{
			EnsureClone(project);
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.Edit; }
		}

	}
}

