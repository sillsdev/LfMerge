// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using SIL.Linux.Logging;
using System.Collections.Generic;

namespace LfMerge.Logging
{
	public class SyslogLogger : LoggerBase
	{
		private SIL.Linux.Logging.SyslogLogger _logger;

		public SyslogLogger()
		{
			_logger = new SIL.Linux.Logging.SyslogLogger();
		}

		private SyslogPriority SeverityToPriority(LogSeverity severity)
		{
			// If integer values of LogSeverity enum ever change, this function will need to be more complicated
			int severityNum = (int)severity;
			return (SyslogPriority)severityNum;
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
			_logger.LogMany(SeverityToPriority(severity), messages);
		}

		public override void Log(LogSeverity severity, string message)
		{
			_logger.Log(SeverityToPriority(severity), message);
		}
	}
}

