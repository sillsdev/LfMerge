// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Core.DataConverters;
using LfMerge.Core.Logging;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.Actions
{
	public class TransferMongoToFdoAction: Action
	{
		private FdoCache _cache;
		private IFdoServiceLocator _servLoc;
		private IMongoConnection _connection;
		private MongoProjectRecordFactory _projectRecordFactory;
		private ILfProject _lfProject;
		private MongoProjectRecord _projectRecord;

		private ILfProjectConfig _lfProjectConfig;

		public EntryCounts EntryCounts { get; set; }

		public TransferMongoToFdoAction(LfMergeSettings settings, ILogger logger, IMongoConnection conn, MongoProjectRecordFactory factory, EntryCounts entryCounts) : base(settings, logger)
		{
			EntryCounts = entryCounts;
			_connection = conn;
			_projectRecordFactory = factory;
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		protected override void DoRun(ILfProject project)
		{
			_lfProject = project;
			_projectRecord = _projectRecordFactory.Create(_lfProject);
			if (_projectRecord == null)
			{
				Logger.Warning("No project named {0}", _lfProject.ProjectCode);
				Logger.Warning("If we are unit testing, this may not be an error");
				return;
			}
			_lfProjectConfig = _projectRecord.Config;
			if (_lfProjectConfig == null)
				return;

			if (project.FieldWorksProject == null)
			{
				Logger.Error("Failed to find the corresponding FieldWorks project!");
				return;
			}
			if (project.FieldWorksProject.IsDisposed)
				Logger.Warning("Project {0} is already disposed; this shouldn't happen", project.ProjectCode);
			_cache = project.FieldWorksProject.Cache;
			if (_cache == null)
			{
				Logger.Error("Failed to find the FDO cache!");
				return;
			}

			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Logger.Error("Failed to find the service locator; giving up.");
				return;
			}

			var converter = new ConvertMongoToFdoLexicon(Settings, project, Logger, _connection, _projectRecord, EntryCounts);
			converter.RunConversion();
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
