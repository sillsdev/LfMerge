// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using Autofac;
using Chorus.Model;
using LibFLExBridgeChorusPlugin;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LfMerge.Queues;
using LfMerge.FieldWorks;

namespace LfMerge
{
	public class MainClass
	{
		public static IContainer Container { get; internal set; }

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<InternetCloneSettingsModel>().AsSelf();
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<UpdateBranchHelperFlex>().As<UpdateBranchHelperFlex>();
			containerBuilder.RegisterType<FlexHelper>().SingleInstance().AsSelf();
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
				LfMergeSettings.LoadSettings();

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectName in clonedQueue)
					{
						queue.DequeueProject(projectName);
						var project = LanguageForgeProject.Create(projectName);

						for (var action = queue.CurrentAction;
							action != null;
							action = action.NextAction)
						{
							action.Run(project);
						}
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
