// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;

namespace LfMerge.Logging
{
	public interface ILogger
	{
		// Base functions that must be implemented by any implementation
		void Log(LogSeverity severity, string message);
		void LogMany(LogSeverity severity, IEnumerable<string> messages);

		// Convenience functions defined by LoggerBase
		void Emergency(string messageFormat, params object[] messageParts);
		void Alert(string messageFormat, params object[] messageParts);
		void Critical(string messageFormat, params object[] messageParts);
		void Error(string messageFormat, params object[] messageParts);
		void Warning(string messageFormat, params object[] messageParts);
		void Notice(string messageFormat, params object[] messageParts);
		void Info(string messageFormat, params object[] messageParts);
		void Debug(string messageFormat, params object[] messageParts);
	}
}

