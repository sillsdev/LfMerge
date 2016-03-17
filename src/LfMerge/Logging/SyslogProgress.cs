// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Palaso.Progress;

namespace LfMerge.Logging
{
	public class SyslogProgress : GenericProgress
	{
		public ILogger Logger { get; private set; }

		public SyslogProgress(ILogger logger, bool verbose = false)
		{
			Logger = logger;
			ShowVerbose = verbose;
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

