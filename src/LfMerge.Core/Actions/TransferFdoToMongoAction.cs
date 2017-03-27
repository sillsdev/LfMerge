// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge.Core.DataConverters;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.Actions
{
	public class TransferFdoToMongoAction: Action
	{
		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		private FdoCache _cache;
		private IFdoServiceLocator _servLoc;
		private IMongoConnection _connection;
		private MongoProjectRecordFactory _projectRecordFactory;

		private ConvertFdoToMongoLexicon _lexiconConverter;

		public TransferFdoToMongoAction(LfMergeSettings settings, ILogger logger, IMongoConnection conn, MongoProjectRecordFactory projectRecordFactory) : base(settings, logger)
		{
			_connection = conn;
			_projectRecordFactory = projectRecordFactory;
		}

		protected override void DoRun(ILfProject project)
		{
			// TODO: These checks might be overkill; consider removing some of them
			if (project == null)
			{
				Logger.Error("Project was null in TransferFdoToMongoAction.DoRun");
				return;
			}
			Logger.Debug("TransferFdoToMongoAction: locating FieldWorks project");
			FwProject fwProject = project.FieldWorksProject;
			if (fwProject == null)
			{
				Logger.Error("Can't find FieldWorks project {0}", project.ProjectCode);
				return;
			}
			Logger.Debug("TransferFdoToMongoAction: locating FieldWorks project cache");
			_cache = fwProject.Cache;
			if (_cache == null)
			{
				Logger.Error("Can't find cache for FieldWorks project {0}", project.ProjectCode);
				return;
			}
			Logger.Debug("TransferFdoToMongoAction: connecting to FieldWorks service locator");
			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Logger.Error("Can't find service locator for FieldWorks project {0}", project.ProjectCode);
				return;
			}

			Logger.Debug("TransferFdoToMongoAction: setting up lexicon converter");
			_lexiconConverter = new ConvertFdoToMongoLexicon(project, Logger, _connection, Progress, _projectRecordFactory);
			Logger.Debug("TransferFdoToMongoAction: about to run lexicon conversion");
			_lexiconConverter.RunConversion();

			Logger.Debug("TransferFdoToMongoAction: successful transfer; setting last-synced date");
			_connection.SetLastSyncedDate(project, DateTime.UtcNow);
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
