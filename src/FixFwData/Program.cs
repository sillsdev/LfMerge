// Copyright (c) 2011-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: Program.cs
// Responsibility: FLEx team

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Palaso.Reporting;
using SIL.FieldWorks.FixData;
using SIL.Utils;

#if LINUX
using Palaso.PlatformUtilities;
using SIL.Linux.Logging;
#endif

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
				var data = new FwDataFixer(pathname, prog, logError, getErrorCount);
				data.FixErrorsAndSave();
			}
			return errorsOccurred ? 1 : 0;
		}

#if LINUX
		private static SyslogLogger logger = null;
#endif
		private static bool errorsOccurred = false;
		private static int errorCount = 0;

		private static void logError(string description, bool errorFixed)
		{
#if LINUX
			if (logger == null)
				Console.WriteLine(description);
			else
				logger.Error(description);
#else
			Console.WriteLine(description);
#endif
			errorsOccurred = true;
			if (errorFixed)
				++errorCount;
		}

		private static int getErrorCount()
		{
			return errorCount;
		}

		private static void SetUpErrorHandling()
		{
			ErrorReport.EmailAddress = "flex_errors@sil.org";
			ErrorReport.AddStandardProperties();
#if LINUX
			if (Platform.IsUnix && Environment.GetEnvironmentVariable("DISPLAY") == null)
				ExceptionHandler.Init(new SyslogExceptionHandler("FixFwData"));
			else
#endif
				ExceptionHandler.Init();
#if LINUX
			if (Platform.IsUnix)
				logger = new SyslogLogger("FixFwData");
#endif
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
#if LINUX
					if (logger == null)
						Console.Out.WriteLine(value);
					else
						logger.Info(value);
#else
					Console.Out.WriteLine(value);
#endif
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
