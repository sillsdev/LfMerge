// Copyright (c) 2016 SIL International
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
		public static string ProjectFolderPath;

		private const string testProjectCode = "testlangproj";
		private const int originalNumOfFdoEntries = 63;
		private const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
		private TestEnvironment env;
		private MongoConnectionDouble mongoConnection;
		private MongoProjectRecordFactory _recordFactory;
		private HgRepository _hgRepo;
		private LanguageForgeProject lfProject;
		private LanguageForgeProject lDProject;
		private TemporaryFolder languageDepotFolder;
		private string lDProjectFolderPath;
		private Guid testEntryGuid;
		private TransferFdoToMongoAction transferFdoToMongo;
		private SynchronizeAction sutSynchronize;

		[SetUp]
		public void Setup()
		{
			env = new TestEnvironment();
			lfProject = LanguageForgeProject.Create(env.Settings, testProjectCode);
			ProjectFolderPath = Path.Combine(env.Settings.WebWorkDirectory, testProjectCode);
			FdoTestFixture.CopyFwProjectTo(testProjectCode, env.Settings.WebWorkDirectory);
			testEntryGuid = Guid.Parse(testEntryGuidStr);

			languageDepotFolder = new TemporaryFolder("SyncTestLD");
			LfMergeSettingsIni lDSettings = new LfMergeSettingsDouble(languageDepotFolder.Path);
			lDProject = LanguageForgeProject.Create(lDSettings, testProjectCode);
			lDProjectFolderPath = Path.Combine(lDSettings.WebWorkDirectory, testProjectCode);
			Directory.CreateDirectory(lDSettings.WebWorkDirectory);
			FdoTestFixture.CopyFwProjectTo(testProjectCode, lDSettings.WebWorkDirectory);
			_hgRepo = new HgRepository(lDProjectFolderPath, false, new NullProgress());
			_hgRepo.Init();

			mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (mongoConnection == null)
				throw new AssertionException("Sync tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Sync tests need a mock MongoProjectRecordFactory in order to work.");

			transferFdoToMongo = new TransferFdoToMongoAction(env.Settings, env.Logger, mongoConnection);
			sutSynchronize = new SynchronizeAction(env.Settings, env.Logger);
		}

		[TearDown]
		public void Teardown()
		{
			env.Dispose();
			Directory.Delete(languageDepotFolder.Path, true);
		}

		[Test]
		public void SynchronizeAction_NoCloneNoChangedData_GlossUnchanged()
		{
			// Exercise
			sutSynchronize.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}

		[Test]
		public void SynchronizeAction_MongoChangedData_GlossChanged()
		{
			// Setup
			TransferFdoToMongoAction.InitialClone = true;
			transferFdoToMongo.Run(lfProject);
			IEnumerable<LfLexEntry> originalMongoData = mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string changedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = changedGloss;

			var cache = lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));
			LanguageForgeProject.DisposeProjectCache(lDProject.ProjectCode);

			// Exercise
			sutSynchronize.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(changedGloss));

			cache = lDProject.FieldWorksProject.Cache;
			lDFdoEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
		}

		[Test]
		public void SynchronizeAction_LDChangedData_GlossChanged()
		{
			// Setup
			TransferFdoToMongoAction.InitialClone = true;
			transferFdoToMongo.Run(lfProject);
			IEnumerable<LfLexEntry> originalMongoData = mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string changedGloss = unchangedGloss + " - changed in LD";

			var cache = lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			int wsEn = cache.WritingSystemFactory.GetWsFromStr("en");
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					lDFdoEntry.SensesOS[0].Gloss.set_String(wsEn, changedGloss);
				});
			cache.ActionHandlerAccessor.Commit();
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
			LanguageForgeProject.DisposeProjectCache(lDProject.ProjectCode);
			string fwdataFilePath = Path.Combine(lDProjectFolderPath, lDProject.ProjectCode + SharedConstants.FwXmlExtension);
			var projectConfig = new ProjectFolderConfiguration(lDProjectFolderPath);
			FlexFolderSystem.ConfigureChorusProjectFolder(projectConfig);
			FLEx.ProjectSplitter.PushHumptyOffTheWall(new NullProgress(), false, fwdataFilePath);
			_hgRepo.AddAndCheckinFiles(projectConfig.IncludePatterns, projectConfig.ExcludePatterns, "changed test gloss");

			// Exercise
			sutSynchronize.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(changedGloss));

			cache = lDProject.FieldWorksProject.Cache;
			lDFdoEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(changedGloss));
		}

	}
}
