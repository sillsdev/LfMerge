// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Logging
{
	public abstract class LoggerBase : ILogger
	{
		protected LoggerBase()
		{
		}

		// Log a single message, already formatted. See Emergency, Alert, etc. functions
		// if you want to get functionst that act more like Console.WriteLine().
		public abstract void Log(LogSeverity severity, string message);

		// Log multiple messages at once, so that they will all end up in the log in one
		// large block, without log messages from other programs interspersed. Most useful
		// if you are collecting errors during a long-running process, and want to log all
		// the errors at the end of the process.
		public abstract void LogMany(LogSeverity severity, IEnumerable<string> messages);

		// ***** Convenience functions for trivially replacing Console.WriteLine() *****

		public void Emergency(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Emergency, String.Format(messageFormat, messageParts));
		}

		public void Alert(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Alert, String.Format(messageFormat, messageParts));
		}

		public void Critical(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Critical, String.Format(messageFormat, messageParts));
		}

		public void Error(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Error, String.Format(messageFormat, messageParts));
		}

		public void Warning(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Warning, String.Format(messageFormat, messageParts));
		}

		public void Notice(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Notice, String.Format(messageFormat, messageParts));
		}

		public void Info(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Info, String.Format(messageFormat, messageParts));
		}

		public void Debug(string messageFormat, params object[] messageParts)
		{
			Log(LogSeverity.Debug, String.Format(messageFormat, messageParts));
		}
	}
}

