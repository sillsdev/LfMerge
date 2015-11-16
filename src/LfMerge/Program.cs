// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Queues;
using LfMerge.FieldWorks;
using System.Threading;
using System.Collections.Generic;

namespace LfMerge
{
	public class MainClass
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
			//string hardCodedBaseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			string hardCodedMongoDbHostName = "languageforge.local";
			//LfMergeSettings.Initialize(hardCodedBaseDir);
			MongoConnection.Initialize(hardCodedMongoDbHostName);

			try
			{
				LfMergeSettings.LoadSettings();
				// TODO: Move this testing code where it belongs
				var localProjectCode = "TestLangProj";
				var thisProject = LanguageForgeProject.Create(localProjectCode);
				var foo = new Actions.UpdateFdoFromMongoDbAction();
				foo.Run(thisProject);
				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						queue.DequeueProject(projectCode);
						var project = LanguageForgeProject.Create(projectCode);

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
				Cleanup();
			}
		}

		/// <summary>
		/// Clean up anything needed before quitting, e.g. disposing of IDisposable objects.
		/// </summary>
		private static void Cleanup()
		{
			LanguageForgeProject.DisposeProjectCache();
		}

	}
}
