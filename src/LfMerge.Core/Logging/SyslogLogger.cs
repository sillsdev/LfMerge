// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using SIL.Linux.Logging;
using SIL.PlatformUtilities;

namespace LfMerge.Core.Logging
{
	public class SyslogLogger : LoggerBase
	{
		private SIL.Linux.Logging.SyslogLogger _logger;

		public SyslogLogger(string programName = null)
		{
			if (Platform.IsLinux)
			{
				_logger = new SIL.Linux.Logging.SyslogLogger(programName ?? "LfMerge");
			}
		}

		private SyslogPriority SeverityToPriority(LogSeverity severity)
		{
			// If integer values of LogSeverity enum ever change, this function will need to be more complicated
			int severityNum = (int)severity;
			return (SyslogPriority)severityNum;
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
			if (_logger == null)
			{
				Console.WriteLine(string.Join(Environment.NewLine, messages));
			}
			else
			{
				_logger.LogMany(SeverityToPriority(severity), messages);
			}
		}

		public override void Log(LogSeverity severity, string message)
		{
			if (_logger == null)
			{
				Console.WriteLine(message);
			}
			else
			{
				_logger.Log(SeverityToPriority(severity), message);
			}
		}
	}
}

