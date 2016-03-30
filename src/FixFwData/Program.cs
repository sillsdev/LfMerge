﻿// Copyright (c) 2011-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: Program.cs
// Responsibility: FLEx team

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using Palaso.Reporting;
using SIL.FieldWorks.FixData;
using SIL.Linux.Logging;
using SIL.Utils;

namespace FixFwData
{
	class Program
	{
		[SuppressMessage("Gendarme.Rules.Portability", "ExitCodeIsLimitedOnUnixRule",
			Justification = "Appears to be a bug in Gendarme...not recognizing that 0 and 1 are in correct range (0..255)")]
		private static int Main(string[] args)
		{
			SetUpErrorHandling();
			var pathname = args[0];
			using (var prog = new LoggingProgress())
			{
				var data = new FwDataFixer(pathname, prog, logError);
				data.FixErrorsAndSave();
			}
			if (errorsOccurred)
				return 1;
			return 0;
		}

		private static SyslogLogger logger = null;
		private static bool errorsOccurred;

		private static void logError(string guid, string date, string description)
		{
			if (logger == null)
				Console.WriteLine(description);
			else
				logger.Error("Error in GUID {0}: {1} (on {2})", guid, description, date);
			errorsOccurred = true;
		}

		private static void SetUpErrorHandling()
		{
			ErrorReport.EmailAddress = "flex_errors@sil.org";
			ErrorReport.AddStandardProperties();
			if (MiscUtils.IsUnix && Environment.GetEnvironmentVariable("DISPLAY") == null)
				ExceptionHandler.Init(new SyslogExceptionHandler("FixFwData"));
			else
				ExceptionHandler.Init();
			if (MiscUtils.IsUnix)
				logger = new SyslogLogger("FixFwData");
		}

		private sealed class LoggingProgress : IProgress, IDisposable
		{
			public event CancelEventHandler Canceling;

			public void Step(int amount)
			{
				if (Canceling != null)
				{
					// don't do anything -- this just shuts up the compiler about the
					// event handler never being used.
				}
			}

			public string Title { get; set; }

			public string Message
			{
				get { return null; }
				set
				{
					if (logger == null)
						Console.Out.WriteLine(value);
					else
						logger.Info(value);
				}
			}

			public int Position { get; set; }
			public int StepSize { get; set; }
			public int Minimum { get; set; }
			public int Maximum { get; set; }
			public ISynchronizeInvoke SynchronizeInvoke { get; private set; }
			public bool IsIndeterminate
			{
				get { return false; }
				set { }
			}

			public bool AllowCancel
			{
				get { return false; }
				set { }
			}
			#region Gendarme required cruft
#if DEBUG
			/// <summary/>
			~LoggingProgress()
			{
				Dispose(false);
			}
#endif

			/// <summary/>
			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			/// <summary/>
			private void Dispose(bool fDisposing)
			{
				System.Diagnostics.Debug.WriteLineIf(!fDisposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			}
			#endregion
		}
	}
}