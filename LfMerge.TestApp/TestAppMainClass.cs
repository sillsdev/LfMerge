// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using CommandLine;
using IniParser.Model;

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
			LfMergeSettingsIni.ConfigDir = folder;

			MainClass.Container = MainClass.RegisterTypes().Build();
			var settings = MainClass.Container.Resolve<ILfMergeSettings>();
			var config = new IniData();
			var main = config.Global;
			main["BaseDir"] = folder;
			((LfMergeSettingsIni)settings).Initialize(config);

			var queueDir = settings.GetQueueDirectory(options.QueueName);
			Directory.CreateDirectory(queueDir);
			File.WriteAllText(Path.Combine(queueDir, options.ProjectCode), string.Empty);

			MainClass.Main(new string[0]);
		}
	}
}
