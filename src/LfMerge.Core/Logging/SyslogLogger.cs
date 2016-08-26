// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
#if __MonoCS__
using SIL.Linux.Logging;
#endif

namespace LfMerge.Core.Logging
{
	public class SyslogLogger : LoggerBase
	{
#if __MonoCS__
		private SIL.Linux.Logging.SyslogLogger _logger;
#endif

		public SyslogLogger(string programName = null)
		{
#if __MonoCS__
			_logger = new SIL.Linux.Logging.SyslogLogger(programName ?? "LfMerge");
#endif
		}

#if __MonoCS__
		private SyslogPriority SeverityToPriority(LogSeverity severity)
		{
			// If integer values of LogSeverity enum ever change, this function will need to be more complicated
			int severityNum = (int)severity;
			return (SyslogPriority)severityNum;
		}
#endif

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
#if __MonoCS__
			_logger.LogMany(SeverityToPriority(severity), messages);
#endif
		}

		public override void Log(LogSeverity severity, string message)
		{
#if __MonoCS__
			_logger.Log(SeverityToPriority(severity), message);
#endif
		}
	}
}

