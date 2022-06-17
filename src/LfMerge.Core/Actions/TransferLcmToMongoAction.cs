// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using SIL.LCModel;

namespace LfMerge.Core.Actions
{
	public class TransferLcmToMongoAction: Action
	{
		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		private LcmCache _cache;
		private ILcmServiceLocator _servLoc;
		private IMongoConnection _connection;
		private MongoProjectRecordFactory _projectRecordFactory;

		private ConvertLcmToMongoLexicon _lexiconConverter;

		public TransferLcmToMongoAction(LfMergeSettings settings, ILogger logger, IMongoConnection conn, MongoProjectRecordFactory projectRecordFactory) : base(settings, logger)
		{
			_connection = conn;
			_projectRecordFactory = projectRecordFactory;
		}

		protected override void DoRun(ILfProject project)
		{
			// TODO: These checks might be overkill; consider removing some of them
			if (project == null)
			{
				Logger.Error("Project was null in TransferLcmToMongoAction.DoRun");
				return;
			}
			Logger.Debug("TransferLcmToMongoAction: locating FieldWorks project");
			FwProject fwProject = project.FieldWorksProject;
			if (fwProject == null)
			{
				Logger.Error("Can't find FieldWorks project {0}", project.ProjectCode);
				return;
			}
			Logger.Debug("TransferLcmToMongoAction: locating FieldWorks project cache");
			_cache = fwProject.Cache;
			if (_cache == null)
			{
				Logger.Error("Can't find cache for FieldWorks project {0}", project.ProjectCode);
				return;
			}
			Logger.Debug("TransferLcmToMongoAction: connecting to FieldWorks service locator");
			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Logger.Error("Can't find service locator for FieldWorks project {0}", project.ProjectCode);
				return;
			}

			Logger.Debug("TransferLcmToMongoAction: setting up lexicon converter");
			_lexiconConverter = new ConvertLcmToMongoLexicon(project, Logger, _connection, Progress, _projectRecordFactory);
			Logger.Debug("TransferLcmToMongoAction: about to run lexicon conversion");
			var errors = _lexiconConverter.RunConversion();
			//todo error handling

			Logger.Debug("TransferLcmToMongoAction: successful transfer; setting last-synced date");
			_connection.SetLastSyncedDate(project, DateTime.UtcNow);
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
