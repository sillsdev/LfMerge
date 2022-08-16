// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Core.Logging
{
	/// <summary>
	/// A logger that outputs to stdout and stderr
	/// </summary>
	public class ConsoleLogger : LoggerBase
	{
		public LogSeverity StderrThreshhold { get; set; }

		public ConsoleLogger() : this(LogSeverity.Warning) { }

		public ConsoleLogger(LogSeverity stderrThreshhold)
		{
			StderrThreshhold = stderrThreshhold;
		}

		public override void Log(LogSeverity severity, string message)
		{
			Log(severity, message, DateTime.UtcNow);
		}

		public void Log(LogSeverity severity, string message, DateTime now)
		{
			// LogSeverity values are lower for more severe messages, higher for less severe
			var output = (severity <= StderrThreshhold) ? Console.Error : Console.Out;
			output.WriteLine($"{now.ToString("o")} {message}");
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
			LogMany(severity, messages, DateTime.UtcNow);
		}

		public void LogMany(LogSeverity severity, IEnumerable<string> messages, DateTime now)
		{
			var output = (severity <= StderrThreshhold) ? Console.Error : Console.Out;
			foreach (var message in messages)
			{
				output.WriteLine($"{now.ToString("o")} {message}");
			}
		}
	}
}
