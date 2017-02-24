// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;

namespace LfMerge.Core.Logging
{
	/// <summary>
	/// A logger that throws away its messages. Useful for unit testing.
	/// </summary>
	public class NullLogger : LoggerBase
	{
		public NullLogger()
		{
		}

		public override void Log(LogSeverity severity, string message)
		{
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
		}
	}
}

