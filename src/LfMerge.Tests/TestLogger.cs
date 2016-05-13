// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using LfMerge.Logging;

namespace LfMerge.Tests
{
	public class TestLogger: LoggerBase
	{
		public TestLogger(string testName = null)
		{
			if (string.IsNullOrEmpty(testName))
				testName = Path.GetRandomFileName();
			LogFileName = Path.Combine(Path.GetTempPath(), testName + ".log");
			using (var writer = File.CreateText(LogFileName))
				LogOneLine(writer, "Starting " + testName);
		}

		public string LogFileName { get; private set; }

		private static void LogOneLine(TextWriter writer, string message)
		{
			var now = DateTime.Now;
			writer.WriteLine("{0}:{1}:{2}: {3}", now.Hour, now.Minute, now.Second, message);
		}

		#region implemented abstract members of LoggerBase

		public override void Log(LogSeverity severity, string message)
		{
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
}

