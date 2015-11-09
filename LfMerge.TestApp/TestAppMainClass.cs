// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using CommandLine;
using CommandLine.Text;
using System.Diagnostics;

namespace LfMerge.TestApp
{
	class TestAppMainClass
	{
		public static void Main(string[] args)
		{
			var options = ParseCommandLineArgs(args);
			if (options == null)
				return;

			Run(options);
		}

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			if (Parser.Default.ParseArguments(args, options))
			{
				if (!options.ShowHelp)
				{
					return options;
				}
			}
			// Display the default usage information
			Console.WriteLine(options.GetUsage());
			return null;
		}

		public static void Run(Options options)
		{
			var folder = Path.Combine(Path.GetTempPath(), "LfMerge.TestApp");
			LfMergeSettings.ConfigDir = folder;
			LfMergeSettings.Initialize(folder);
			LfMergeSettings.Current.SaveSettings();

			var queueDir = LfMergeSettings.Current.GetQueueDirectory(options.QueueName);
			Directory.CreateDirectory(queueDir);
			File.WriteAllText(Path.Combine(queueDir, options.ProjectCode), string.Empty);

//			var startInfo = new ProcessStartInfo {
//				Arguments = "--debug LfMerge.exe",
//				FileName = "mono",
//				UseShellExecute = true,
//			};
//			Process.Start(startInfo);
			MainClass.Main(new string[0]);
		}
	}
}
