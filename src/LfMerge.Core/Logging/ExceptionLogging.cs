// Copyright (c) 2016 Eberhard Beilharz
// Copyright (c) 2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Bugsnag;
using Bugsnag.Clients;
using Palaso.PlatformUtilities;

namespace LfMerge.Core.Logging
{
	public class ExceptionLogging: BaseClient
	{
		private ExceptionLogging(string apiKey, string executable, string callerFilePath)
			: base(apiKey)
		{
			Setup(callerFilePath, executable);
		}

		private void Setup(string callerFilePath, string executable)
		{
			var solutionPath = Path.GetFullPath(Path.Combine(callerFilePath, "../../../"));
			Config.FilePrefixes = new[] { solutionPath };
			Config.BeforeNotify(OnBeforeNotify);
			Config.StoreOfflineErrors = true;

			Config.Metadata.AddToTab("App", "executable", executable);
			Config.Metadata.AddToTab("App", "runtime", Platform.IsMono ? "Mono" : ".NET");

			// The BaseClient class deals with catching unhandled exceptions
		}

		public void AddInfo(string projectCode, string modelVersion)
		{
			Config.Metadata.AddToTab("App", "Project", projectCode);
			Config.Metadata.AddToTab("App", "ModelVersion", modelVersion);
		}

		private string RemoveFileNamePrefix(string fileName)
		{
			var result = fileName;
			return string.IsNullOrEmpty(result) ? result :
				Config.FilePrefixes.Aggregate(result, (current, prefix) => current.Replace(prefix, string.Empty));
		}

		private bool OnBeforeNotify(Event error)
		{
			var stackTrace = new StackTrace(error.Exception, true);
			if (stackTrace.FrameCount <= 0)
				return true;

			var frame = stackTrace.GetFrame(0);
			// During development the line number probably changes frequently, but we want
			// to treat all errors with the same exception in the same method as being the
			// same, even when the line numbers differ, so we set it to 0. For releases
			// we can assume the line number to be constant for a released build.
			var linenumber = Config.ReleaseStage == "development" ? 0 : frame.GetFileLineNumber();
			error.GroupingHash = string.Format("{0} {1} {2} {3}", error.Exception.GetType().Name,
				RemoveFileNamePrefix(frame.GetFileName()), frame.GetMethod().Name, linenumber);

			return true;
		}

		public static void Initialize(string apiKey, string executable,
			[CallerFilePath] string filename = null)
		{
			Client = new ExceptionLogging(apiKey, executable, filename);
		}

		public static ExceptionLogging Client { get; private set; }
	}
}

