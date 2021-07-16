// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LfMerge.Core.Logging;

namespace LfMerge.Core.Tests
{
	public class TestLogger: LoggerBase
	{
		private StringBuilder _errors;
		private StringBuilder _all;

		public TestLogger(string testName = null)
		{
			if (string.IsNullOrEmpty(testName))
				testName = Path.GetRandomFileName();
			LogFileName = Path.Combine(Path.GetTempPath(), testName + ".log");
			_errors = new StringBuilder();
			_all = new StringBuilder();
			using (var writer = File.CreateText(LogFileName))
				LogOneLine(writer, "Starting " + testName);
		}

		public string LogFileName { get; }

		public string Errors => _errors.ToString();

		public string Messages => _all.ToString();

		private void LogOneLine(TextWriter writer, string message)
		{
			var now = DateTime.Now;
			writer.WriteLine("{0:D2}:{1:D2}:{2:D2}: {3}", now.Hour, now.Minute, now.Second, message);
			_all.AppendLine(message);
			Console.WriteLine(message);
		}

		#region implemented abstract members of LoggerBase

		public override void Log(LogSeverity severity, string message)
		{
			if (severity <= LogSeverity.Error)
				_errors.AppendLine(message);

			using (var writer = File.AppendText(LogFileName))
			{
				LogOneLine(writer, message);
			}
		}

		public override void LogMany(LogSeverity severity, IEnumerable<string> messages)
		{
			using (var writer = File.AppendText(LogFileName))
			{
				foreach (var message in messages)
					LogOneLine(writer, message);
			}
		}

		#endregion
	}

	public static class LoggerExtensions
	{
		public static string GetErrors(this ILogger logger)
		{
			return ((TestLogger)logger).Errors;
		}

		public static string GetMessages(this ILogger logger)
		{
			return ((TestLogger)logger).Messages;
		}
	}
}

