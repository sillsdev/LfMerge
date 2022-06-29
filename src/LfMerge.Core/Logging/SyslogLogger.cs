// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Core.Logging
{
	public class SyslogLogger : LoggerBase
	{
		public SyslogLogger(string programName = null)
		{
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
			Console.WriteLine(string.Join(Environment.NewLine, messages));
		}

		public override void Log(LogSeverity severity, string message)
		{
			Console.WriteLine(message);
		}
	}
}

