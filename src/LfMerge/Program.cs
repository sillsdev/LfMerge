// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using Autofac;
using Chorus.Model;
using LibFLExBridgeChorusPlugin;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LfMerge.Queues;
using LfMerge.FieldWorks;
using LfMerge.Actions;
using SIL.IO.FileLock;
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

			Container = RegisterTypes().Build();
			Stream s = typeof(MainClass).Assembly.GetManifestResourceStream(typeof(MainClass), "GOLDEtic.xml");
			foreach (GoldEticItem item in GoldEticXmlParser.ParseXml(s))
			{
				foreach (GoldEticItem item2 in item.Subitems)
					Console.WriteLine("Item {0} is called \"{1}\"", item2.Guid, item2.ORCDelimitedNameByWs("en"));
			}

			var fileLock = SimpleFileLock.CreateFromFilePath(LfMergeSettings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					Console.WriteLine("Can't acquire file lock - is another instance running?");
					return;
				}

				LfMergeSettings.LoadSettings();
				MongoConnection.Initialize(LfMergeSettings.Current.MongoDbHostNameAndPort);
				// TODO: Move this testing code where it belongs
				var localProjectCode = "TestLangProj";
				var thisProject = LanguageForgeProject.Create(localProjectCode);
				var foo = Container.ResolveKeyed<IAction>(ActionNames.UpdateMongoDbFromFdo);
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
