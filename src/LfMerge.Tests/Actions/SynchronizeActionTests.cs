// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge;
using LfMerge.Actions;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Reporting;
using LfMerge.Settings;
using LfMerge.Tests;
using NUnit.Framework;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests.Actions
{
	[TestFixture, Category("LongRunning"), Category("IntegrationTests")]
	public class SynchronizeActionTests
	{
		public static string LDProjectFolderPath;

		/* testlangproj is the original LD repo
		 * testlangproj-modified contains:
		 * 	a modified entry - ztestmain
		 * 		lexeme form - Kal: underlying form - changed in FW
		 * 		Sense 1 Gloss - en: English gloss - changed in FW
		 * 	a deleted entry - ken
		 * 	an added entry - Ira
		 * 		Sense 1 Gloss - en: Externally referenced picture
		 * 				File: /home/ira/Pictures/test images/TestImage.jpg
		*/
		private const string testProjectCode = "testlangproj";
		private const string modifiedTestProjectCode = "testlangproj-modified";
		private const int originalNumOfFdoEntries = 63;
		private const int deletedEntries = 1;
		private const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
		private const string testCreatedEntryGuidStr = "ba8076a9-6552-46b2-a14a-14c01191453b";
		private const string testDeletedEntryGuidStr = "c5f97698-dade-4ba0-9f91-580ab19ff411";

		private TestEnvironment _env;
		private MongoConnectionDouble _mongoConnection;
		private EntryCounts _counts;
		private MongoProjectRecordFactory _recordFactory;
		private LanguageForgeProject _lfProject;
		private LanguageDepotMock _lDProject;
		private LfMergeSettingsIni _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private Guid _testEntryGuid;
		private Guid _testCreatedEntryGuid;
		private Guid _testDeletedEntryGuid;
		private TransferFdoToMongoAction _transferFdoToMongo;

		public static MercurialServer LDServer { get; set; }

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_env.Settings.CommitWhenDone = true;
			_counts = MainClass.Container.Resolve<EntryCounts>();
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			TestEnvironment.CopyFwProjectTo(testProjectCode, _env.Settings.WebWorkDirectory);

			// Guids are named for the diffs for the modified test project
			_testEntryGuid = Guid.Parse(testEntryGuidStr);
			_testCreatedEntryGuid = Guid.Parse(testCreatedEntryGuidStr);
			_testDeletedEntryGuid = Guid.Parse(testDeletedEntryGuidStr);

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
		}

		[TearDown]
		public void Teardown()
		{
			if (_lfProject != null)
			LanguageForgeProject.DisposeFwProject(_lfProject);
			if (_lDProject != null)
			LanguageDepotMock.DisposeFwProject(_lDProject);
			if (_languageDepotFolder != null)
			_languageDepotFolder.Dispose();
			_env.Dispose();
			_mongoConnection.Reset();
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_NoChangedData_GlossUnchanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);
			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}

		private string GetGlossFromFdo(Guid testEntryGuid, int expectedSensesCount)
		{
			var cache = _lfProject.FieldWorksProject.Cache;
			var lfFdoEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			Assert.That(lfFdoEntry, Is.Not.Null);
			Assert.That(lfFdoEntry.SensesOS.Count, Is.EqualTo(expectedSensesCount));
			return lfFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text;
		}

		private string GetGlossFromMongoDb(Guid testEntryGuid,
			int expectedLfLexEntriesCount = originalNumOfFdoEntries,
			int expectedDeletedEntries = deletedEntries)
		{
			// _mongoConnection.GetLfLexEntries() returns an enumerator, so we shouldn't
			// introduce a local variable for it because then we'd enumerate it multiple times
			// which might cause problems.
			Assert.That(_mongoConnection.GetLfLexEntries(), Is.Not.Null);
			Assert.That(_mongoConnection.GetLfLexEntries().Count(data => !data.IsDeleted),
				Is.EqualTo(expectedLfLexEntriesCount));
			Assert.That(_mongoConnection.GetLfLexEntries().Count(data => data.IsDeleted),
				Is.EqualTo(expectedDeletedEntries));
			var lfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.IsDeleted, Is.EqualTo(false));
			return lfEntry.Senses[0].Gloss["en"].Value;
		}

		private string GetGlossFromLanguageDepot(Guid testEntryGuid, int expectedSensesCount)
		{
			// Since there is no direct way to check the XML files checked in to Mercurial, we
			// do it indirectly by re-cloning the repo from LD and checking the new clone.
			_lfProject.FieldWorksProject.Dispose();
			Directory.Delete(_lfProject.ProjectDir, true);
			var ensureClone = new EnsureCloneAction(_env.Settings, _env.Logger);
			ensureClone.Run(_lfProject);
			return GetGlossFromFdo(testEntryGuid, expectedSensesCount);
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_LFDataChanged_GlossChanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string lfChangedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = lfChangedGloss;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			Assert.That(GetGlossFromFdo(_testEntryGuid, 2), Is.EqualTo(lfChangedGloss));
			Assert.That(GetGlossFromMongoDb(_testEntryGuid, expectedDeletedEntries: 0),
				Is.EqualTo(lfChangedGloss));
			Assert.That(GetGlossFromLanguageDepot(_testEntryGuid, 2), Is.EqualTo(lfChangedGloss));
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_LDDataChanged_GlossChanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string ldChangedGloss = unchangedGloss + " - changed in FW";

			lfEntry = originalMongoData.First(e => e.Guid == _testDeletedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("ken"));

			int createdEntryCount = originalMongoData.Count(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(createdEntryCount, Is.EqualTo(0));

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(ldChangedGloss));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			Assert.That(GetGlossFromFdo(_testEntryGuid, 2), Is.EqualTo(ldChangedGloss));
			Assert.That(GetGlossFromMongoDb(_testEntryGuid), Is.EqualTo(ldChangedGloss));

			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			lfEntry = receivedMongoData.First(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("Ira"));

			int deletedEntryCount = receivedMongoData.Count(e => e.Guid == _testDeletedEntryGuid && !e.IsDeleted);
			Assert.That(deletedEntryCount, Is.EqualTo(0));

			Assert.That(GetGlossFromLanguageDepot(_testEntryGuid, 2), Is.EqualTo(ldChangedGloss));
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDDataChanged_LFWins()
		{
			//Setup
			TestEnvironment.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			DateTime originalLfDateModified = lfEntry.DateModified;

			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string fwChangedGloss = unchangedGloss + " - changed in FW";
			string lfChangedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = lfChangedGloss;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(fwChangedGloss));
			DateTime originalLdDateModified = lDFdoEntry.DateModified;

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			Assert.That(GetGlossFromMongoDb(_testEntryGuid), Is.EqualTo(lfChangedGloss));
			LfLexEntry updatedLfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == _testEntryGuid);
			DateTime updatedLfDateModified = updatedLfEntry.DateModified;
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLfDateModified));
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLdDateModified));
		}

		[Test]
		public void SynchronizeAction_LFDataDeleted_EntryRemoved()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			DateTime originalLfDateModified = lfEntry.DateModified;

			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;

			// Don't use _mongoConnection.RemoveRecord to delete the entry.  LF uses the "IsDeleted" field
			lfEntry.IsDeleted = true;
			// The LF PHP code would have updated DateModified when it deleted the record, so simulate that here
			lfEntry.DateModified = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);
			IEnumerable<LfLexEntry> updatedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(updatedMongoData.First(e => e.Guid == _testEntryGuid).IsDeleted, Is.True);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));
			DateTime originalLdDateModified = lDFdoEntry.DateModified;

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			// Deleting entries in LF should *not* remove them, just set the isDeleted flag
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));
			var entry = receivedMongoData.FirstOrDefault(e => e.Guid ==_testEntryGuid);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.IsDeleted, Is.EqualTo(true));
			DateTime updatedLfDateModified = entry.DateModified;
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLfDateModified));
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLdDateModified));

			var cache = _lfProject.FieldWorksProject.Cache;
			Assert.That(()=> cache.ServiceLocator.GetObject(_testEntryGuid),
				Throws.InstanceOf<KeyNotFoundException>());
		}

		[Test]
		public void SynchronizeAction_LFDataDeletedLDDataChanged_LDWins()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			DateTime originalLfDateModified = lfEntry.DateModified;

			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string fwChangedGloss = unchangedGloss + " - changed in FW";

			// Don't use _mongoConnection.RemoveRecord to delete the entry.  LF uses the "IsDeleted" field
			lfEntry.IsDeleted = true;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);
			IEnumerable<LfLexEntry> updatedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(updatedMongoData.First(e => e.Guid == _testEntryGuid).IsDeleted, Is.True);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(fwChangedGloss));
			DateTime originalLdDateModified = lDFdoEntry.DateModified;

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify LD modified entry remains and LF marks not deleted
			Assert.That(GetGlossFromMongoDb(_testEntryGuid), Is.EqualTo(fwChangedGloss));
			Assert.That(GetGlossFromLanguageDepot(_testEntryGuid, 2), Is.EqualTo(fwChangedGloss));
			LfLexEntry updatedLfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == _testEntryGuid);
			DateTime updatedLfDateModified = updatedLfEntry.DateModified;
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLfDateModified));
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLdDateModified));
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDDataDeleted_LFWins()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testDeletedEntryGuid);
			DateTime originalLfDateModified = lfEntry.DateModified;
			Assert.That(lfEntry.Senses.Count, Is.EqualTo(1));
			const string lfCreatedGloss = "new English gloss - added in LF";
			const string fwChangedGloss = "English gloss - changed in FW";
			// LF adds a gloss to the entry that LD is deleting
			lfEntry.Senses[0].Gloss = LfMultiText.FromSingleStringMapping("en", lfCreatedGloss);
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			Assert.That(()=> lDcache.ServiceLocator.GetObject(_testDeletedEntryGuid),
				Throws.InstanceOf<KeyNotFoundException>());
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(fwChangedGloss));
			DateTime originalLdDateModified = lDFdoEntry.DateModified;

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify modified LF entry wins
			Assert.That(GetGlossFromMongoDb(_testDeletedEntryGuid, originalNumOfFdoEntries + 1, 0),
				Is.EqualTo(lfCreatedGloss));
			Assert.That(GetGlossFromMongoDb(_testEntryGuid, originalNumOfFdoEntries + 1, 0),
				Is.EqualTo(fwChangedGloss));
			Assert.That(GetGlossFromLanguageDepot(_testDeletedEntryGuid, 1), Is.EqualTo(lfCreatedGloss));
			LfLexEntry updatedLfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == _testEntryGuid);
			DateTime updatedLfDateModified = updatedLfEntry.DateModified;
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLfDateModified));
			Assert.That(updatedLfDateModified, Is.GreaterThan(originalLdDateModified));
		}

		[Test]
		public void TransferMongoToFdoAction_NoChangedData_DateModifiedUnchanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);
			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			// Exercise
			var transferMongoToFdo = new TransferMongoToFdoAction(_env.Settings, _env.Logger,
				_mongoConnection, _recordFactory, _counts);
			transferMongoToFdo.Run(_lfProject);

			// Verify
			var lfcache = _lfProject.FieldWorksProject.Cache;
			var lfFdoEntry = lfcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lfFdoEntry.DateModified.ToUniversalTime(),
				Is.EqualTo(DateTime.Parse("2016-02-25 03:51:29.404")));
		}

	}
}
