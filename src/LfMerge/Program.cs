// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using LfMerge.Queues;
using LfMerge.FieldWorks;
using Autofac;

namespace LfMerge
{
	class MainClass
	{
		public static IContainer Container { get; internal set; }

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			LfMerge.Actions.Action.Register(containerBuilder);
			Queue.Register(containerBuilder);
			return containerBuilder;
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			Container = RegisterTypes().Build();

			try
			{
				// TODO: read settings from config instead of hard coding them here
				var baseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
				LfMergeSettings.Initialize(baseDir);

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					foreach (var projectName in queue.QueuedProjects)
					{
						var project = LanguageForgeProject.Create(projectName);

						for (var action = queue.CurrentAction;
							action != null;
							action = action.NextAction)
						{
							action.Run(project);
						}
					}
				}

				var database = args.Length > 1 ? args[0] : "Sena 3";

				using (var fw = new FwProject(database))
				{
					// just some test output
					var fdoCache = fw.Cache;
					Console.WriteLine("Ethnologue Code: {0}", fdoCache.LangProject.EthnologueCode);
					Console.WriteLine("Interlinear texts:");
					foreach (var t in fdoCache.LangProject.InterlinearTexts)
					{
						Console.WriteLine("{0:D6}: title: {1} (comment: {2})", t.Hvo,
							t.Title.BestVernacularAlternative.Text,
							t.Comment.BestVernacularAnalysisAlternative.Text);
					}
				}
			}
			finally
			{
				Container.Dispose();
			}
		}
	}
}
