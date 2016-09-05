// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.LanguageForge.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using Palaso.Progress;

namespace LfMerge.Core
{
	public static class MainClass
	{
		public static IContainer Container { get; internal set; }
		public static ILogger Logger { get; set; }

		static MainClass()
		{
			if (Container == null)
				Container = RegisterTypes().Build();

			Logger = Container.Resolve<ILogger>();
		}

		internal static ContainerBuilder RegisterTypes()
		{
			var containerBuilder = new ContainerBuilder();
			containerBuilder.RegisterType<LfMergeSettings>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<SyslogLogger>().SingleInstance().As<ILogger>()
				.WithParameter(new TypedParameter(typeof(string), "LfMerge"));
			containerBuilder.RegisterType<LanguageDepotProject>().As<ILanguageDepotProject>();
			containerBuilder.RegisterType<ProcessingState.Factory>().As<IProcessingStateDeserialize>();
			containerBuilder.RegisterType<ChorusHelper>().SingleInstance().AsSelf();
			containerBuilder.RegisterType<MongoConnection>().SingleInstance().As<IMongoConnection>().ExternallyOwned();
			containerBuilder.RegisterType<MongoProjectRecordFactory>().AsSelf();
			containerBuilder.RegisterType<EntryCounts>().AsSelf();
			containerBuilder.RegisterType<SyslogProgress>().As<IProgress>();
			containerBuilder.RegisterType<LanguageForgeProxy>().As<ILanguageForgeProxy>();
			Actions.Action.Register(containerBuilder);
			Queue.Register(containerBuilder);
			return containerBuilder;
		}

	}
}

