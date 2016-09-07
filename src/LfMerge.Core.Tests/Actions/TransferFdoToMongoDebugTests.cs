// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.Actions;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using LfMerge.Core.Tests;
using NUnit.Framework;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.Tests.Actions
{
	[TestFixture, Category("LongRunning"), Category("DebugTests")]
	public class TransferFdoToMongoDebugTests
	{
		public static string LDProjectFolderPath;

//		private const string testProjectCode = "testlangproj";
//		private const int originalNumOfFdoEntries = 63;
		private const string testProjectCode = "test-rwr-flex";
		private const int originalNumOfFdoEntries = 53020;

		private TestEnvironment _env;
		private MongoConnection _mongoConnection;
		private MongoProjectRecordFactory _recordFactory;
		private LanguageForgeProject _lfProject;
		private LfMergeSettings _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		public static MercurialServer LDServer { get; set; }

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_env.Settings.CommitWhenDone = true;
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			TestEnvironment.CopyFwProjectTo(testProjectCode, _env.Settings.WebWorkDirectory);

			_languageDepotFolder = new TemporaryFolder("DebugTestLD");
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LDProjectFolderPath = Path.Combine(_lDSettings.WebWorkDirectory, testProjectCode);

			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnection;
			if (_mongoConnection == null)
				throw new AssertionException("Debug test needs a real MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactory;
			if (_recordFactory == null)
				throw new AssertionException("Debug test needs a real MongoProjectRecordFactory in order to work.");
		}

		[TearDown]
		public void Teardown()
		{
			if (_lfProject != null)
			{
				_mongoConnection.GetProjectDatabase(_lfProject).DropCollection("lexicon");
				LanguageForgeProject.DisposeFwProject(_lfProject);
			}
			if (_languageDepotFolder != null)
				_languageDepotFolder.Dispose();
			_env.Dispose();
			_env = null;
//			_mongoConnection.Reset();
		}

//		[Test, Explicit("Debug transfer of large flex projects to mongo")]
		[Test]
		public void TransferFdoToMongoAction_LargeFdoData_GetTiming()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);
			_lfProject.IsInitialClone = true;

			// Exercise
			var sutTransferFdoToMongo = new TransferFdoToMongoAction(_env.Settings, _env.Logger, _mongoConnection);
			sutTransferFdoToMongo.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetRecords<LfLexEntry>(_lfProject, "lexicon"); // .GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));
		}

	}
}
