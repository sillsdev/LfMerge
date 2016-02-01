// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using Autofac;
using Chorus.Model;
using LfMerge.Actions;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using LfMerge.Queues;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using SIL.IO.FileLock;

namespace LfMerge
{
	public class MainClass
	{
		public static IContainer Container { get; internal set; }

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<LfMergeSettingsIni>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<SyslogLogger>().SingleInstance().As<ILogger>()
				.WithParameter(new TypedParameter(typeof(string), "LfMerge"));
			containerBuilder.RegisterType<InternetCloneSettingsModel>().AsSelf();
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<UpdateBranchHelperFlex>().As<UpdateBranchHelperFlex>();
			containerBuilder.RegisterType<FlexHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<MongoConnection>().SingleInstance().As<IMongoConnection>().ExternallyOwned();
			containerBuilder.RegisterType<MongoProjectRecordFactory>().AsSelf();
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

			if (Container == null)
				Container = RegisterTypes().Build();

			var settings = Container.Resolve<LfMergeSettingsIni>();
			var fileLock = SimpleFileLock.CreateFromFilePath(settings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					Console.WriteLine("Can't acquire file lock - is another instance running?");
					return;
				}

				MongoConnection.Initialize(settings.MongoDbHostNameAndPort, "scriptureforge"); // TODO: Database name should come from config

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						queue.DequeueProject(projectCode);
						var project = LanguageForgeProject.Create(settings, projectCode);

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
				if (fileLock != null)
					fileLock.ReleaseLock();

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
