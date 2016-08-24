// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using MongoDB.Bson;
using MongoDB.Driver;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Actions
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

		private ConvertFdoToMongoLexicon _lexiconConverter;

		public TransferFdoToMongoAction(LfMergeSettingsIni settings, ILogger logger, IMongoConnection conn) : base(settings, logger)
		{
			_connection = conn;
		}

		protected override void DoRun(ILfProject project)
		{
			// TODO: These checks might be overkill; consider removing some of them
			FwProject fwProject = project.FieldWorksProject;
			if (fwProject == null)
			{
				Logger.Error("Can't find FieldWorks project {0}", project.ProjectCode);
				return;
			}
			_cache = fwProject.Cache;
			if (_cache == null)
			{
				Logger.Error("Can't find cache for FieldWorks project {0}", project.ProjectCode);
				return;
			}
			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Logger.Error("Can't find service locator for FieldWorks project {0}", project.ProjectCode);
				return;
			}

			_lexiconConverter = new ConvertFdoToMongoLexicon(project, Logger, _connection);
			_lexiconConverter.RunConversion();
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
