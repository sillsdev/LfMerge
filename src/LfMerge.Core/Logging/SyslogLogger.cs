// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
#if LINUX
using SIL.Linux.Logging;
#endif

namespace LfMerge.Core.Logging
{
	public class SyslogLogger : LoggerBase
	{
#if LINUX
		private SIL.Linux.Logging.SyslogLogger _logger;
#endif

		public SyslogLogger(string programName = null)
		{
#if LINUX
			_logger = new SIL.Linux.Logging.SyslogLogger(programName ?? "LfMerge");
#endif
		}

#if LINUX
		private SyslogPriority SeverityToPriority(LogSeverity severity)
		{
			// If integer values of LogSeverity enum ever change, this function will need to be more complicated
			int severityNum = (int)severity;
			return (SyslogPriority)severityNum;
		}
#endif

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
#if LINUX
			_logger.LogMany(SeverityToPriority(severity), messages);
#endif
		}

		public override void Log(LogSeverity severity, string message)
		{
#if LINUX
			_logger.Log(SeverityToPriority(severity), message);
#endif
		}
	}
}

