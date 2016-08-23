// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using MongoDB.Driver;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Actions
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

		public LfMerge.DataConverters.EntryCounts EntryCounts { get; set; }

		public TransferMongoToFdoAction(LfMergeSettingsIni settings, ILogger logger, IMongoConnection conn, MongoProjectRecordFactory factory) : base(settings, logger)
		{
			EntryCounts = new EntryCounts();
			_connection = conn;
			_projectRecordFactory = factory;
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.SYNCING; }
		}

		protected override void DoRun(ILfProject project)
		{
			// TODO: Some of these checks might be overkill; consider removing some of them
			Logger.Debug("MongoToFdo: starting");
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

			var converter = new ConvertMongoToFdoLexicon(Settings, project, Logger, _connection, _projectRecord);
			converter.RunConversion();
			EntryCounts = converter.EntryCounts;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
