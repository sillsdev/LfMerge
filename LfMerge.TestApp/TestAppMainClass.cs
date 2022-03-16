// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using Autofac;
using IniParser.Model;
using LfMerge.Core;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;

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
			System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_BaseDir, folder);

			MainClass.Container = MainClass.RegisterTypes().Build();
			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			settings.Initialize();

			var queueDir = settings.GetQueueDirectory(QueueNames.Synchronize);
			Directory.CreateDirectory(queueDir);
			File.WriteAllText(Path.Combine(queueDir, options.ProjectCode), string.Empty);

			Program.Main(new string[0]);
		}
	}
}
