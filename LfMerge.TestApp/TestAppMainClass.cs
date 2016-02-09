// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using CommandLine;
using IniParser.Model;
using LfMerge.Queues;
using LfMerge.Settings;

namespace LfMerge.TestApp
{
	class TestAppMainClass
	{
		public static void Main(string[] args)
		{
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			var folder = Path.Combine(Path.GetTempPath(), "LfMerge.TestApp");
			LfMergeSettingsIni.ConfigDir = folder;

			MainClass.Container = MainClass.RegisterTypes().Build();
			var settings = MainClass.Container.Resolve<LfMergeSettingsIni>();
			var config = new IniData();
			var main = config.Global;
			main["BaseDir"] = folder;
			settings.Initialize(config);

			var queueDir = settings.GetQueueDirectory(QueueNames.Synchronize);
			Directory.CreateDirectory(queueDir);
			File.WriteAllText(Path.Combine(queueDir, options.PriorityProject), string.Empty);

			MainClass.Main(new string[0]);
		}
	}
}
