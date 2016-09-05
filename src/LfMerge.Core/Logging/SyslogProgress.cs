// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge.Core.Settings;
using Palaso.Progress;

namespace LfMerge.Core.Logging
{
	public class SyslogProgress : GenericProgress
	{
		public ILogger Logger { get; private set; }

		public SyslogProgress(ILogger logger, LfMergeSettings settings)
		{
			Logger = logger;
			ShowVerbose = settings.VerboseProgress;
		}

		public override void WriteMessage(string message, params object[] args)
		{
			Logger.Log(LogSeverity.Info, SafeFormat(message, args));
		}

		public override void WriteMessageWithColor(string colorName, string message, params object[] args)
		{
			// Ignore color in syslog messages
			WriteMessage(message, args);
		}
	}
}

