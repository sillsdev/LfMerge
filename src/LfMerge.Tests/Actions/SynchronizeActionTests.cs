﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using Chorus.sync;
using Chorus.VcsDrivers.Mercurial;
using LibFLExBridgeChorusPlugin;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;
using LfMerge;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using LfMerge.Tests;
using LfMerge.Tests.Fdo;
using NUnit.Framework;
using Palaso.Progress;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	[TestFixture, Explicit, Category("LongRunning")]
	public class SynchronizeActionTests
	{
		public static string LDProjectFolderPath;

		private const string testProjectCode = "testlangproj";
		private const string testProjectCode2 = "testlangproj2";
		private const int originalNumOfFdoEntries = 63;
		private const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
		private TestEnvironment _env;
		private MongoConnectionDouble _mongoConnection;
		private MongoProjectRecordFactory _recordFactory;
		private LanguageForgeProject _lfProject;
		private LanguageForgeProject _lDProject;
		private LfMergeSettingsIni _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private Guid _testEntryGuid;
		private TransferFdoToMongoAction _transferFdoToMongo;
		private SynchronizeAction _sutSynchronize;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _env.Settings.WebWorkDirectory);
			_testEntryGuid = Guid.Parse(testEntryGuidStr);

			_languageDepotFolder = new TemporaryFolder("SyncTestLD");
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LDProjectFolderPath = Path.Combine(_lDSettings.WebWorkDirectory, testProjectCode);

			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_mongoConnection == null)
				throw new AssertionException("Sync tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Sync tests need a mock MongoProjectRecordFactory in order to work.");

			_transferFdoToMongo = new TransferFdoToMongoAction(_env.Settings, _env.Logger, _mongoConnection);
			_sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
		}

		[TearDown]
		public void Teardown()
		{
			_env.Dispose();
			Directory.Delete(_languageDepotFolder.Path, true);
		}

		[Test]
		public void SynchronizeAction_NoCloneNoChangedData_GlossUnchanged()
		{
			//Setup
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}

		[Test]
		public void SynchronizeAction_MongoDataChanged_GlossChanged()
		{
			// Setup
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			TransferFdoToMongoAction.InitialClone = true;
			_transferFdoToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string changedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = changedGloss;

			LanguageForgeProject.DisposeProjectCache(_lfProject.ProjectCode);
			_lDProject = LanguageForgeProject.Create(_lDSettings, testProjectCode);
			var cache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));
			LanguageForgeProject.DisposeProjectCache(_lDProject.ProjectCode);
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(changedGloss));

			LanguageForgeProject.DisposeProjectCache(_lfProject.ProjectCode);
			_lDProject = LanguageForgeProject.Create(_lDSettings, testProjectCode);
			cache = _lDProject.FieldWorksProject.Cache;
			lDFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
		}

		[Test]
		public void SynchronizeAction_LDDataChanged_GlossChanged()
		{
			// Setup
			FdoTestFixture.CopyFwProjectTo(testProjectCode2, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, testProjectCode2), LDProjectFolderPath);

			TransferFdoToMongoAction.InitialClone = true;
			_transferFdoToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string changedGloss = unchangedGloss + " - changed in FW";

			LanguageForgeProject.DisposeProjectCache(_lfProject.ProjectCode);
			_lDProject = LanguageForgeProject.Create(_lDSettings, testProjectCode);
			var cache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
			LanguageForgeProject.DisposeProjectCache(_lDProject.ProjectCode);
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			cache = _lfProject.FieldWorksProject.Cache;
			var lfFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lfFdoEntry, Is.Not.Null);
			Assert.That(lfFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lfFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));

			lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(changedGloss));

			LanguageForgeProject.DisposeProjectCache(_lfProject.ProjectCode);
			_lDProject = LanguageForgeProject.Create(_lDSettings, testProjectCode);
			cache = _lDProject.FieldWorksProject.Cache;
			lDFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
		}

	}
}