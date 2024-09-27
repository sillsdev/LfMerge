// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using NUnit.Framework;
using SIL.LCModel;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests.Actions
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class SynchronizeActionTests
	{
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
		private const int originalNumOfLcmEntries = 63;
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
		private FwProject _fwProject;
		private LfMergeSettings _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private TemporaryFolder _fwFolder;
		private Guid _testEntryGuid;
		private Guid _testCreatedEntryGuid;
		private Guid _testDeletedEntryGuid;
		private TransferLcmToMongoAction _transferLcmToMongo;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_env.Settings.CommitWhenDone = true;
			_counts = MainClass.Container.Resolve<EntryCounts>();
			_lfProject = LanguageForgeProject.Create(testProjectCode);
			TestEnvironment.CopyFwProjectTo(testProjectCode, _env.Settings.WebWorkDirectory);

			// Guids are named for the diffs for the modified test project
			_testEntryGuid = Guid.Parse(testEntryGuidStr);
			_testCreatedEntryGuid = Guid.Parse(testCreatedEntryGuidStr);
			_testDeletedEntryGuid = Guid.Parse(testDeletedEntryGuidStr);

			_languageDepotFolder = new TemporaryFolder("SyncTestLD" + Path.GetRandomFileName());
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LanguageDepotMock.ProjectFolderPath = Path.Combine(_lDSettings.WebWorkDirectory, testProjectCode);

			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_mongoConnection == null)
				throw new AssertionException("Sync tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Sync tests need a mock MongoProjectRecordFactory in order to work.");

			_transferLcmToMongo = new TransferLcmToMongoAction(_env.Settings, _env.Logger, _mongoConnection, _recordFactory);
			LanguageDepotMock.Server = new MercurialServer(LanguageDepotMock.ProjectFolderPath);
		}

		[TearDown]
		public void Teardown()
		{
			if (LanguageDepotMock.Server != null)
			{
				LanguageDepotMock.Server.Stop();
				LanguageDepotMock.Server = null;
			}

			if (_lfProject != null)
				LanguageForgeProject.DisposeFwProject(_lfProject);
			if (_lDProject != null)
				LanguageDepotMock.DisposeFwProject(_lDProject);
			if (_fwProject != null && !_fwProject.IsDisposed)
				_fwProject.Dispose();
			_languageDepotFolder?.Dispose();
			_fwFolder?.Dispose();
			_env.Dispose();
			_mongoConnection.Reset();
		}

		public FwProject CreateTestFwProject(string projectCode)
		{
			_fwFolder = new TemporaryFolder(_lDSettings.BaseDir.Replace("SyncTestLD", "SyncTestFW"));
			var fwSettings = new LfMergeSettingsDouble(_fwFolder.Path);
			TestEnvironment.CopyFwProjectTo(projectCode, _lDSettings.WebWorkDirectory); // Mock LD server
			TestEnvironment.CopyFwProjectTo(projectCode, fwSettings.WebWorkDirectory); // Local FW project that will be modified and pushed
			return new FwProject(fwSettings, projectCode);
		}

		public void CommitAndPush(FwProject fwProject, string commitMsg = "SyncActionTest commit")
		{
			if (!File.Exists(fwProject.FwdataPath)) throw new InvalidOperationException($"Fwdata file {fwProject.FwdataPath} should exist");
			if (!LanguageDepotMock.Server.IsStarted) LanguageDepotMock.Server.Start();
			fwProject.Cache.ActionHandlerAccessor.Commit();
			if (!fwProject.IsDisposed) fwProject.Dispose();
			LfMergeBridge.LfMergeBridge.DisassembleFwdataFile(new SIL.Progress.NullProgress(), false, fwProject.FwdataPath);
			MercurialTestHelper.HgClean(fwProject.ProjectDir); // Ensure ConfigurationSettings, etc., don't get committed
			MercurialTestHelper.HgCommit(fwProject.ProjectDir, commitMsg);
			MercurialTestHelper.HgPush(fwProject.ProjectDir, LanguageDepotMock.Server.Url);
		}

		[Test]
		public void SampleTest()
		{
			_fwProject = CreateTestFwProject(testProjectCode);
			var entry = LcmTestHelper.GetEntry(_fwProject, _testEntryGuid);
			LcmTestHelper.UpdateAnalysisText(_fwProject, entry.SensesOS[0].Gloss, gloss => gloss + " - changed in FW");
			CommitAndPush(_fwProject);
			// Now we would run the rest of the test code, doing a S/R and verifying its results
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_NoChangedData_GlossUnchanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfLcmEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}

		private string GetGlossFromLcm(Guid testEntryGuid, int expectedSensesCount)
		{
			var cache = _lfProject.FieldWorksProject.Cache;
			var lfLcmEntry = cache.ServiceLocator.GetObject(testEntryGuid) as ILexEntry;
			Assert.That(lfLcmEntry, Is.Not.Null);
			Assert.That(lfLcmEntry.SensesOS, Is.Not.Null);
			Assert.That(lfLcmEntry.SensesOS.Count, Is.EqualTo(expectedSensesCount));
			return lfLcmEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text;
		}

		private string GetGlossFromMongoDb(Guid testEntryGuid,
			int expectedLfLexEntriesCount = originalNumOfLcmEntries,
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
			var ensureClone = new EnsureCloneAction(_env.Settings, _env.Logger, _recordFactory, _mongoConnection);
			ensureClone.Run(_lfProject);
			return GetGlossFromLcm(testEntryGuid, expectedSensesCount);
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_LFDataChanged_GlossChanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string lfChangedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = lfChangedGloss;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(testProjectCode, _lDSettings);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDLcmEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDLcmEntry, Is.Not.Null);
			Assert.That(lDLcmEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDLcmEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			Assert.That(GetGlossFromLcm(_testEntryGuid, 2), Is.EqualTo(lfChangedGloss));
			Assert.That(GetGlossFromMongoDb(_testEntryGuid, expectedDeletedEntries: 0),
				Is.EqualTo(lfChangedGloss));
			Assert.That(GetGlossFromLanguageDepot(_testEntryGuid, 2), Is.EqualTo(lfChangedGloss));
		}

		[Test, Explicit("Superceeded by later tests")]
		public void SynchronizeAction_LDDataChanged_GlossChanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LanguageDepotMock.ProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string ldChangedGloss = unchangedGloss + " - changed in FW";

			lfEntry = originalMongoData.First(e => e.Guid == _testDeletedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("ken"));

			int createdEntryCount = originalMongoData.Count(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(createdEntryCount, Is.EqualTo(0));

			_lDProject = new LanguageDepotMock(testProjectCode, _lDSettings);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDLcmEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDLcmEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(ldChangedGloss));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			Assert.That(GetGlossFromLcm(_testEntryGuid, 2), Is.EqualTo(ldChangedGloss));
			Assert.That(GetGlossFromMongoDb(_testEntryGuid), Is.EqualTo(ldChangedGloss));

			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			lfEntry = receivedMongoData.First(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("Ira"));

			int deletedEntryCount = receivedMongoData.Count(e => e.Guid == _testDeletedEntryGuid && !e.IsDeleted);
			Assert.That(deletedEntryCount, Is.EqualTo(0));

			Assert.That(GetGlossFromLanguageDepot(_testEntryGuid, 2), Is.EqualTo(ldChangedGloss));
		}

		// Returns original AuthorInfo.ModifiedDate, followed by original DateModified. (This order is chosen because DateModified is only useful in delete tests)
		// Also returns the entry so that calling code can grab the updated date they want.
		public (DateTime, DateTime, LfLexEntry) UpdateLfEntry(LanguageForgeProject lfProject, Guid entryId, Action<LfLexEntry> updater)
		{
			var lfEntry = _mongoConnection.GetLfLexEntryByGuid(lfProject, entryId);
			Assert.That(lfEntry, Is.Not.Null);
			updater(lfEntry);
			// Remember that in LfMerge it's AuthorInfo that corresponds to the Lcm modified date
			DateTime origModifiedDate = lfEntry.AuthorInfo.ModifiedDate;
			DateTime origDateModified = lfEntry.DateModified;
			lfEntry.AuthorInfo.ModifiedDate = lfEntry.DateModified = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(lfProject, lfEntry);
			return (origModifiedDate, origDateModified, lfEntry);
		}

		// Returns original AuthorInfo.ModifiedDate, followed by *updated* AuthorInfo.ModifiedDate. Does not return either the old or new DateModified values.
		public (string, DateTime, DateTime) UpdateLfEntry(LanguageForgeProject lfProject, Guid entryId, Func<LfLexEntry, LfStringField> getField, Func<string, string> textConverter)
		{
			string unchangedValue = null;
			var (origModifiedDate, _, lfEntry) = UpdateLfEntry(lfProject, entryId, lfEntry => {
				unchangedValue = getField(lfEntry).Value;
				getField(lfEntry).Value = textConverter(unchangedValue);
			});
			return (unchangedValue, origModifiedDate, lfEntry.AuthorInfo.ModifiedDate);
		}

		// Returns original DateModified, followed by *updated* DateModified. Does not return either the old or new AuthorInfo.ModifiedDate values.
		public (DateTime, DateTime) DeleteLfEntry(LanguageForgeProject lfProject, Guid entryId)
		{
			var (_, origDateModified, lfEntry) = UpdateLfEntry(lfProject, entryId, lfEntry => {
				lfEntry.IsDeleted = true;
			});
			return (origDateModified, lfEntry.DateModified);
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDDataChanged_LFWins()
		{
			// Setup
			// LF project cloned before FW modifications are pushed
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// FW project has changed gloss, pushed after LF project is cloned
			_fwProject = CreateTestFwProject(testProjectCode);
			var entry = LcmTestHelper.GetEntry(_fwProject, _testEntryGuid);
			LcmTestHelper.UpdateAnalysisText(_fwProject, entry.SensesOS[0].Gloss, gloss => gloss + " - changed in FW");
			CommitAndPush(_fwProject);

			// Now LF project changes gloss
			var (unchangedGloss, origDateModified, origAuthorInfoModifiedDate) = UpdateLfEntry(
				_lfProject, _testEntryGuid, entry => entry.Senses[0].Gloss["en"], gloss => gloss + " - changed in LF");

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			var updatedLfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			Assert.That(updatedLfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(unchangedGloss + " - changed in LF"));
			// LF had the same data previously; however it's a merge conflict so DateModified got updated
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(origDateModified));
			// But the LCM modified date (AuthorInfo.ModifiedDate in LF) should be updated.
			Assert.That(updatedLfEntry.AuthorInfo.ModifiedDate, Is.GreaterThan(origAuthorInfoModifiedDate));
			Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}

		[Test]
		public void SynchronizeAction_LFDataDeleted_EntryRemoved()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			var (origDateModified, _) = DeleteLfEntry(_lfProject, _testEntryGuid);

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			// Deleting entries in LF should *not* remove them, just set the isDeleted flag
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfLcmEntries));
			var entry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.IsDeleted, Is.EqualTo(true));
			Assert.That(entry.DateModified, Is.GreaterThan(origDateModified));

			// And entry is gone from LF's copy of the FW project as well
			var cache = _lfProject.FieldWorksProject.Cache;
			Assert.That(()=> cache.ServiceLocator.GetObject(_testEntryGuid),
				Throws.InstanceOf<KeyNotFoundException>());
			Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}

		[Test]
		public void SynchronizeAction_LFEntryDeletedLDDataChanged_LDWins()
		{
			// Setup
			// LF project cloned before FW modifications are pushed
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// FW project has changed gloss, pushed after LF project is cloned
			_fwProject = CreateTestFwProject(testProjectCode);
			var entry = LcmTestHelper.GetEntry(_fwProject, _testEntryGuid);
			var unchangedGloss = LcmTestHelper.UpdateAnalysisText(_fwProject, entry.SensesOS[0].Gloss, gloss => gloss + " - changed in FW");
			CommitAndPush(_fwProject);

			// Now LF project deletes entry
			var (origDateModified, dateModifiedAfterDeletion) = DeleteLfEntry(_lfProject, _testEntryGuid);

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			var updatedLfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			Assert.That(updatedLfEntry.IsDeleted, Is.False);
			Assert.That(updatedLfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(unchangedGloss + " - changed in FW"));
			// LF entry's modified date updated when it was restored from being deleted
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(origDateModified));
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(dateModifiedAfterDeletion));
			Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}

		[Test]
		public void SynchronizeAction_LFDataDeletedLDDataChanged_LDWins()
		{
			// Setup
			// LF project cloned before FW modifications are pushed
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// FW project has changed gloss, pushed after LF project is cloned
			_fwProject = CreateTestFwProject(testProjectCode);
			var entry = LcmTestHelper.GetEntry(_fwProject, _testEntryGuid);
			var unchangedGloss = LcmTestHelper.UpdateAnalysisText(_fwProject, entry.SensesOS[0].Gloss, gloss => gloss + " - changed in FW");
			CommitAndPush(_fwProject);

			// Now LF project deletes sense from entry
			var lfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			var origDateModified = lfEntry.DateModified;
			var origAuthorInfoModifiedDate = lfEntry.AuthorInfo.ModifiedDate;

			lfEntry.Senses.Remove(lfEntry.Senses[0]);
			var dateModifiedAfterRemoval = DateTime.UtcNow; // Modified date should be *later* than FW change
			lfEntry.DateModified = lfEntry.AuthorInfo.ModifiedDate = dateModifiedAfterRemoval;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			var updatedLfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			Assert.That(updatedLfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(unchangedGloss + " - changed in FW"));
			// LF entry's modified date updated when the sense was restored from being deleted
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(origDateModified));
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(dateModifiedAfterRemoval));
			Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDEntryDeleted_LFWins()
		{
			// Setup
			// LF project cloned before FW modifications are pushed
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// FW project deletes entry
			_fwProject = CreateTestFwProject(testProjectCode);
			LcmTestHelper.DeleteEntry(_fwProject, _testDeletedEntryGuid);
			CommitAndPush(_fwProject);

			// Meanwhile LF project changes gloss of same entry
			var (origDateModified, _, _) = UpdateLfEntry(_lfProject, _testDeletedEntryGuid,
				entry => entry.Senses[0].Gloss = LfMultiText.FromSingleStringMapping("en", "new English gloss - added in LF"));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			var updatedLfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testDeletedEntryGuid);
			Assert.That(updatedLfEntry, Is.Not.Null);
			Assert.That(updatedLfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("new English gloss - added in LF"));
			// LF had the same data previously; however it's a merge conflict so DateModified got updated
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(origDateModified));
			Assert.That(updatedLfEntry.AuthorInfo.ModifiedDate, Is.GreaterThan(origDateModified));
			Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDOtherDataChanged_ModifiedDateUpdated()
		{
			// Setup
			// LF project cloned before FW modifications are pushed
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// FW project has changed gloss
			_fwProject = CreateTestFwProject(testProjectCode);
			var entry = LcmTestHelper.GetEntry(_fwProject, _testEntryGuid);
			var unchangedGloss = LcmTestHelper.UpdateAnalysisText(_fwProject, entry.SensesOS[0].Gloss, gloss => gloss + " - changed in FW");
			CommitAndPush(_fwProject);

			// While LF project adds a note to the entry
			var (origDateModified, _, _) = UpdateLfEntry(
				_lfProject, _testEntryGuid, entry => entry.Note = LfMultiText.FromSingleStringMapping("en", "A note from LF"));

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			sutSynchronize.Run(_lfProject);

			// Verify
			var updatedLfEntry = _mongoConnection.GetLfLexEntryByGuid(_lfProject, _testEntryGuid);
			Assert.That(updatedLfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(unchangedGloss + " - changed in FW"));
			Assert.That(updatedLfEntry.Note["en"].Value, Is.EqualTo("A note from LF"));
			// LF had the same data previously; however it's a merge conflict so DateModified got updated
			Assert.That(updatedLfEntry.DateModified, Is.GreaterThan(origDateModified));
			// But the LCM modified date (AuthorInfo.ModifiedDate in LF) should be updated.
			Assert.That(updatedLfEntry.AuthorInfo.ModifiedDate, Is.GreaterThan(origDateModified));
		}

		[Test]
		public void SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync()
		{
			// Setup
			// Buggy code path needs us to change the field writing system to a "magic" ws (it's 0 in the original data/testlangproj project)
			var lcmMetaData = _lfProject.FieldWorksProject.Cache.MetaDataCacheAccessor as SIL.LCModel.Infrastructure.IFwMetaDataCacheManaged;
			int listRef_flid = lcmMetaData.GetFieldIds().FirstOrDefault(flid => lcmMetaData.GetFieldLabel(flid) == "Cust Single ListRef");
			Assert.AreNotEqual(0, listRef_flid, "Cust Single ListRef field not found in test data");
			string fieldLabel = lcmMetaData.GetFieldLabel(listRef_flid);
			string fieldHelp = lcmMetaData.GetFieldHelp(listRef_flid);
			int wsid = SIL.LCModel.DomainServices.WritingSystemServices.kwsAnal;
			lcmMetaData.UpdateCustomField(listRef_flid, fieldHelp, wsid, fieldLabel);

			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// To look at Mongo optionlist before test runs, uncomment this block
			// var x = _mongoConnection.GetLfOptionLists().FirstOrDefault(l => l.Code == "domain-type");
			// if (x != null) {
			// 	foreach (LfOptionListItem item in x.Items) {
			// 		Console.WriteLine($"{item.Guid} ({item.Key}) => {item.Value}");
			// 	}
			// }

			// Buggy code path requires that there not be a GUID in the Mongo data
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			lfEntry.CustomFieldGuids.Remove("customField_entry_Cust_Single_ListRef");

			DateTime originalLfDateModified = lfEntry.DateModified;
			DateTime originalLfAuthorInfoModifiedDate = lfEntry.AuthorInfo.ModifiedDate;
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(testProjectCode, _lDSettings);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDLcmEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			var data = (SIL.LCModel.Application.ISilDataAccessManaged)lDcache.DomainDataByFlid;
			int ownedHvo = data.get_ObjectProp(lDLcmEntry.Hvo, listRef_flid);
			Assert.AreNotEqual(0, ownedHvo, "Custom field value in test data was invalid during setup");
			Assert.IsTrue(data.get_IsValidObject(ownedHvo), "Custom field value in test data was invalid during setup");
			ICmObject referencedObject = lDcache.GetAtomicPropObject(ownedHvo);
			Assert.IsNotNull(referencedObject, "Custom field in test data referenced invalid CmObject during setup");
			DateTime originalLdDateModified = lDLcmEntry.DateModified;

			// Exercise
			var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			var timeBeforeRun = DateTime.UtcNow;
			sutSynchronize.Run(_lfProject);

			// Verify
			LfLexEntry updatedLfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == _testEntryGuid);
			var updatedLcmEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;

			ownedHvo = data.get_ObjectProp(updatedLcmEntry.Hvo, listRef_flid);
			Assert.AreNotEqual(0, ownedHvo, "Custom field value in test data was invalid after running sync");
			Assert.IsTrue(data.get_IsValidObject(ownedHvo), "Custom field value in test data was invalid after running sync");
			referencedObject = lDcache.GetAtomicPropObject(ownedHvo);
			Assert.IsNotNull(referencedObject, "Custom field in test data referenced invalid CmObject after running sync");
			var poss = referencedObject as ICmPossibility;
			// TODO: Write another test to check on the abbrev hierarchy, because we may have a bug here (LfMerge not doing correct optionlist keys for hierarchical items)
			// Console.WriteLine($"Abbrev hierarchy: {poss.AbbrevHierarchyString}");
			Assert.IsNotNull(poss, "Custom field value in test data did not reference a CmPossibility object after running sync");
		}

		[Test]
		public void TransferMongoToLcmAction_NoChangedData_DateModifiedUnchanged()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);
			_lfProject.IsInitialClone = true;
			_transferLcmToMongo.Run(_lfProject);

			// Exercise
			var transferMongoToLcm = new TransferMongoToLcmAction(_env.Settings, _env.Logger,
				_mongoConnection, _recordFactory, _counts);
			transferMongoToLcm.Run(_lfProject);

			// Verify
			var lfcache = _lfProject.FieldWorksProject.Cache;
			var lfLcmEntry = lfcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lfLcmEntry.DateModified.ToUniversalTime(),
				Is.EqualTo(DateTime.Parse("2016-02-25 03:51:29.404")));
		}

	}
}
