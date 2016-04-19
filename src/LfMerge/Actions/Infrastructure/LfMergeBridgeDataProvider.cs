// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.VcsDrivers;
using LfMerge.Settings;
using LfMergeBridge;
using Palaso.Progress;
using ILogger = LfMergeBridge.ILogger;

namespace LfMerge.Actions.Infrastructure
{
	public class LfMergeBridgeDataProvider: ILfMergeBridgeDataProvider
	{
		public LfMergeBridgeDataProvider(LfMergeSettingsIni settings, ILogger logger,
			IProgress progress, ILfProject project)
		{
			Settings = settings;
			Logger = logger;
			Progress = progress;
			Project = project;
		}

		private LfMergeSettingsIni Settings { get; set; }
		private ILfProject Project { get; set; }

		#region ILfMergeBridgeDataProvider implementation

		public RepositoryAddress Repo
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return RepositoryAddress.Create("Language Depot", chorusHelper.GetSyncUri(Project));
			}
		}

		public string ProjectFolderPath
		{
			get
			{
				return Path.Combine(Settings.WebWorkDirectory, Project.ProjectCode);
			}
		}

		public IProgress Progress { get; private set; }

		public string ProjectCode
		{
			get
			{
				return Project.ProjectCode;
			}
		}

		public ILogger Logger { get; private set; }

		public string ChorusUserName
		{
			get
			{
				return Project.LanguageDepotProject.Username;
			}
		}

		#endregion
	}
}

