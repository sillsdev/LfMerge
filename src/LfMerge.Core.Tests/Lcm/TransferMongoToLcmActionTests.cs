// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Model;
using MongoDB.Bson;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LfMerge.Core.Tests.Lcm
{
	public class TransferMongoToLcmActionTests : LcmTestBase
	{
		[Test]
		public void Action_Should_UpdateDefinitions()
		{
			// Setup
			var lfProj = _lfProj;
			var data = new SampleData();
			string newDefinition = "New definition for this unit test";
			data.bsonTestData["senses"][0]["definition"]["en"]["value"] = newDefinition;
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;

			_conn.UpdateMockLfLexEntry(data.bsonTestData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			string expectedShortName = data.bsonTestData["citationForm"].AsBsonDocument.GetElement(0).Value["value"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);

			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));
			Assert.That(entry.ShortName, Is.EqualTo(expectedShortName));
			Assert.That(entry.SensesOS[0].Definition.BestAnalysisAlternative.Text, Is.EqualTo(newDefinition));
		}

		[Test]
		public void Action_ChangedWithSampleData_ShouldUpdatePictures()
		{
			// Setup initial Mongo project has 1 picture and 2 captions
			var lfProj = _lfProj;
			var data = new SampleData();
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			string expectedInternalFileName = Path.Combine("Pictures", data.bsonTestData["senses"][0]["pictures"][0]["fileName"].ToString());
			string expectedExternalFileName = data.bsonTestData["senses"][0]["pictures"][1]["fileName"].ToString();
			int newMongoPictureCount = data.bsonTestData["senses"][0]["pictures"].AsBsonArray.Count;
			int newMongoCaptionCount = data.bsonTestData["senses"][0]["pictures"][0]["caption"].AsBsonDocument.Count();
			Assert.That(newMongoPictureCount, Is.EqualTo(2));
			Assert.That(newMongoCaptionCount, Is.EqualTo(2));

			// Initial LCM project has 63 entries, 3 internal pictures, and 1 externally linked picture
			LcmCache cache = _cache;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int originalNumOfLcmPictures = entryRepo.AllInstances().
				Count(e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(originalNumOfLcmPictures, Is.EqualTo(3+1));
			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);
			var entryBefore = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.That(entryBefore.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entryBefore.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));

			// Exercise adding 1 picture with 2 captions. Note that the picture that was previously attached
			// to this LCM entry will end up being deleted, because it does not have a corresponding picture in LF.
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			SutMongoToLcm.Run(lfProj);

			// Verify "Added" picture is now the only picture on the sense (because the "old" picture was deleted),
			// and that it has 2 captions with the expected values.
			entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int numOfLcmPictures = entryRepo.AllInstances().
				Count(e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(numOfLcmPictures, Is.EqualTo(originalNumOfLcmPictures));

			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));
			Assert.That(entry.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entry.SensesOS.First().PicturesOS.Count, Is.EqualTo(2));
			Assert.That(entry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo(expectedInternalFileName));
			Assert.That(entry.SensesOS[0].PicturesOS[1].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo(expectedExternalFileName));

			LfMultiText expectedNewCaption = ConvertLcmToMongoLexicon.
				ToMultiText(entry.SensesOS[0].PicturesOS[0].Caption, cache.ServiceLocator.WritingSystemManager);
			int expectedNumOfNewCaptions = expectedNewCaption.Count();
			Assert.That(expectedNumOfNewCaptions, Is.EqualTo(2));
			string expectedNewVernacularCaption = expectedNewCaption["qaa-x-kal"].Value;
			string expectedNewAnalysisCaption = expectedNewCaption["en"].Value;
			Assert.That(expectedNewVernacularCaption.Equals("First Vernacular caption"));
			Assert.That(expectedNewAnalysisCaption.Equals("Internal path reference"));

			var testSubEntry = cache.ServiceLocator.GetObject(Guid.Parse(TestSubEntryGuidStr)) as ILexEntry;
			Assert.That(testSubEntry, Is.Not.Null);
			Assert.That(testSubEntry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath,
				Is.EqualTo(string.Format("Pictures{0}TestImage.tif", Path.DirectorySeparatorChar)));
			var kenEntry = cache.ServiceLocator.GetObject(Guid.Parse(KenEntryGuidStr)) as ILexEntry;
			Assert.That(kenEntry, Is.Not.Null);
			Assert.That(kenEntry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath,
				Is.EqualTo(string.Format("F:{0}src{0}xForge{0}web-languageforge{0}test{0}php{0}common{0}TestImage.jpg", Path.DirectorySeparatorChar)));
		}

		[Test]
		public void Action_RunTwice_ShouldNotDuplicatePictures()
		{
			// Setup initial Mongo project has 1 picture and 2 captions
			var lfProj = _lfProj;
			var data = new SampleData();
			int newMongoPictureCount = data.bsonTestData["senses"][0]["pictures"].AsBsonArray.Count;
			int newMongoCaptionCount = data.bsonTestData["senses"][0]["pictures"][0]["caption"].AsBsonDocument.Count();
			Assert.That(newMongoPictureCount, Is.EqualTo(2));
			Assert.That(newMongoCaptionCount, Is.EqualTo(2));

			// Initial LCM project has 63 entries, 3 internal pictures, and 1 externally linked picture
			LcmCache cache = _cache;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int originalNumOfLcmPictures = entryRepo.AllInstances().Count(
				e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(originalNumOfLcmPictures, Is.EqualTo(3+1));
			string expectedGuidStrBefore = data.bsonTestData["guid"].AsString;
			Guid expectedGuidBefore = Guid.Parse(expectedGuidStrBefore);
			var entryBefore = entryRepo.GetObject(expectedGuidBefore);
			Assert.That(entryBefore.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entryBefore.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));

			// Exercise running Action twice
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			SutMongoToLcm.Run(lfProj);
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			SutMongoToLcm.Run(lfProj);

			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);
			var entry = entryRepo.GetObject(expectedGuid);
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));

			Assert.That(entry.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entry.SensesOS.First().PicturesOS.Count, Is.EqualTo(2));
		}

		[Test]
		public void Action_WithEmptyMongoGrammar_ShouldPreserveLcmGrammarEntries()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosBeforeTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosAfterTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest));
			Assert.That(secondPosAfterTest, Is.Not.Null);
			Assert.That(secondPosAfterTest.Guid, Is.EqualTo(secondPosBeforeTest.Guid));
			Assert.That(secondPosAfterTest, Is.SameAs(secondPosBeforeTest));
		}

		[Test]
		public void Action_WithNoChangesFromMongo_ShouldCountZeroChanges()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		[Test]
		public void Action_WithOneNewEntry_ShouldCountOneAdded()
		{
			// Setup
			var lfProj = _lfProj;

			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(1));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry added"));
		}

		[Test]
		public void Action_WithOneModifiedEntry_ShouldCountOneModified()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(1));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry modified"));
		}

		[Test]
		public void Action_WithOneDeletedEntry_ShouldCountOneDeleted()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(1));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry deleted"));
		}

		[Test]
		public void Action_WithTwoNewEntries_ShouldCountTwoAdded()
		{
			// Setup
			var lfProj = _lfProj;

			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			LfLexEntry newEntry2 = new LfLexEntry();
			newEntry2.Guid = Guid.NewGuid();
			string newLexeme2 = "new lexeme #2 for this test";
			newEntry2.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme2);
			newEntry2.AuthorInfo = new LfAuthorInfo();
			newEntry2.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry2.AuthorInfo.ModifiedDate = newEntry2.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry2);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(2));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 2 entries added"));
		}

		[Test]
		public void Action_WithTwoModifiedEntries_ShouldCountTwoModified()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			Guid kenGuid = Guid.Parse(KenEntryGuidStr);
			LfLexEntry kenEntry = _conn.GetLfLexEntryByGuid(kenGuid);
			string changedLexeme2 = "modified lexeme #2 for this test";
			kenEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme2);
			kenEntry.AuthorInfo = new LfAuthorInfo();
			kenEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(kenEntry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(2));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 2 entries modified"));
		}

		[Test]
		public void Action_WithTwoDeletedEntries_ShouldCountTwoDeleted()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);
			Guid kenGuid = Guid.Parse(KenEntryGuidStr);
			entry = _conn.GetLfLexEntryByGuid(kenGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(2));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 2 entries deleted"));
		}

		[Test]
		public void Action_WithOneNewEntry_ShouldNotCountThatNewEntryOnSecondRun()
		{
			// Setup
			var lfProj = _lfProj;

			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(1));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry added"));

			// Exercise again
			SutMongoToLcm.Run(lfProj);

			// Verify zero on second run
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		[Test]
		public void Action_WithOneModifiedEntry_ShouldNotCountThatModifiedEntryOnSecondRun()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(1));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry modified"));

			// Exercise again
			SutMongoToLcm.Run(lfProj);

			// Verify zero on second run
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		[Test]
		public void Action_WithOneDeletedEntry_ShouldNotCountThatDeletedEntryOnSecondRun()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(1));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry deleted"));

			// Exercise again
			SutMongoToLcm.Run(lfProj);

			// Verify zero on second run
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		[Test]
		public void Action_RunTwiceWithOneNewEntryEachTime_ShouldCountTwoAddedInTotal()
		{
			// Setup
			var lfProj = _lfProj;

			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(1));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry added"));

			// Setup second run
			newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			newLexeme = "second new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(1));
			// Modified and Deleted shouldn't have changed, but check Added first
			// since that's the main point of this test
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry added"));
		}

		[Test]
		public void Action_RunTwiceWithTheSameEntryModifiedEachTime_ShouldCountTwoModifiedInTotal()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(1));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry modified"));

			// Setup second run
			string changedLexeme2 = "second modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme2);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise second run
			SutMongoToLcm.Run(lfProj);

			// Verify second run
			Assert.That(_counts.Modified, Is.EqualTo(1));
			// Added and Deleted shouldn't have changed, but check Modified first
			// since that's the main point of this test
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry modified"));
		}

		[Test]
		public void Action_RunTwiceWithTheSameEntryDeletedEachTime_ShouldCountJustOneDeletedInTotal()
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(1));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry deleted"));

			entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise second run
			SutMongoToLcm.Run(lfProj);

			// Verify second run
			Assert.That(_counts.Deleted,  Is.EqualTo(0));
			// Added and Modified shouldn't have changed either, but check Deleted first
			// since that's the main point of this test
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		// TODO: Move custom field tests to their own test class, and move these helper functions with them
		public void AddCustomFieldToSense(LfSense sense, string fieldName, BsonValue fieldValue)
		{
			if (sense.CustomFields == null)
			{
				sense.CustomFields = new BsonDocument();
			}
			if (sense.CustomFieldGuids == null)
			{
				sense.CustomFieldGuids = new BsonDocument();
			}
			sense.CustomFields[fieldName] = fieldValue;
			// Clear out the GUIDs: since LF doesn't add them, we want to test what happens if they're missing
			sense.CustomFieldGuids[fieldName] = new BsonArray();
		}

		public void SetCustomMultiOptionList(LfSense sense, string fieldName, string[] keys)
		{
			BsonValue value = new BsonArray(keys);
			AddCustomFieldToSense(sense, fieldName, new BsonDocument("values", value));
		}

		public IEnumerable<string> GetLcmAbbrevsForField(ILexSense lcmSense, int fieldId)
		{
			var foo = lcmSense.Guid;
			LcmCache cache = _cache;
			int size = cache.DomainDataByFlid.get_VecSize(lcmSense.Hvo, fieldId);
			for (int i = 0; i < size; i++)
			{
				int itemHvo = cache.DomainDataByFlid.get_VecItem(lcmSense.Hvo, fieldId, i);
				ICmObject obj = cache.ServiceLocator.GetObject(itemHvo);
				// Note that we check for CmCustomItemTags.kClassId, *not* CmPossibilityTags.kClassId. This field has a custom list as its target.
				Assert.That(obj.ClassID, Is.EqualTo(CmCustomItemTags.kClassId), "Custom Multi ListRef field in test data should point to CmCustomItem objects, not CmPossibility (or anything else)");
				ICmPossibility poss = obj as ICmPossibility;
				Assert.That(poss, Is.Not.Null);
				Assert.That(poss.Abbreviation, Is.Not.Null);
				ITsString enAbbr = poss.Abbreviation.get_String(_wsEn);
				Assert.That(enAbbr, Is.Not.Null);
				Assert.That(enAbbr.Text, Is.Not.Null);
				yield return enAbbr.Text;
			}
		}

		public void Run_CustomMultiListRefTest(int whichSense, params string[] desiredKeys)
		{
			// Setup
			var lfProj = _lfProj;
			SutLcmToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			LfSense sense = entry.Senses[whichSense];
			SetCustomMultiOptionList(sense, "customField_senses_Cust_Multi_ListRef", desiredKeys);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			LcmCache cache = _cache;

			var lcmEntry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.IsNotNull(lcmEntry);
			Assert.That(cache.ServiceLocator.MetaDataCache.FieldExists(LexSenseTags.kClassName, "Cust Multi ListRef", false), "LexSense should have the Cust Multi ListRef field in our test data.");
			int fieldId = cache.ServiceLocator.MetaDataCache.GetFieldId(LexSenseTags.kClassName, "Cust Multi ListRef", false);
			ILexSense lcmSense = lcmEntry.SensesOS[whichSense];
			IEnumerable<string> lcmAbbrevs = GetLcmAbbrevsForField(lcmSense, fieldId);
			Assert.That(lcmAbbrevs, Is.EquivalentTo(desiredKeys));
		}

		[Test]
		public void CustomMultiListRef_WithTwoOriginalItemsSettingTheSameItems_ShouldRemainUnchanged()
		{
			Run_CustomMultiListRefTest(0, "fci", "sci");
		}

		[Test]
		public void CustomMultiListRef_WithTwoOriginalItemsSettingJustTheFirstItem_ShouldDeleteTheSecond()
		{
			Run_CustomMultiListRefTest(0, "fci");
		}

		[Test]
		public void CustomMultiListRef_WithTwoOriginalItemsSettingJustTheSecondItem_ShouldDeleteTheFirst()
		{
			Run_CustomMultiListRefTest(0, "sci");
		}

		[Test]
		public void CustomMultiListRef_WithTwoOriginalItemsSettingNoneOfThem_ShouldDeleteBoth()
		{
			Run_CustomMultiListRefTest(0);
		}

		[Test]
		public void CustomMultiListRef_WithZeroOriginalItemsSettingTwoItems_ShouldRemainUnchanged()
		{
			Run_CustomMultiListRefTest(1, "fci", "sci");
		}

		[Test]
		public void CustomMultiListRef_WithZeroOriginalItemsSettingJustOneItem_ShouldReturnThatOneItem()
		{
			Run_CustomMultiListRefTest(1, "fci");
		}

		[Test]
		public void CustomMultiListRef_WithZeroOriginalItemsSettingADifferentItem_ShouldAlsoReturnThatOneItem()
		{
			Run_CustomMultiListRefTest(1, "sci");
		}

		[Test]
		public void CustomMultiListRef_WithZeroOriginalItemsSettingNoneOfThem_ShouldAddNone()
		{
			Run_CustomMultiListRefTest(1);
		}


		#if false  // We've changed how we handle OptionLists since these tests were written, and they are no longer valid
		[Test]
		public void Action_WithOneItemInMongoGrammar_ShouldUpdateThatOneItemInLcmGrammar()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosBeforeTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "k");
			grammarEntry.Add("value", "v");
			grammarEntry.Add("abbreviation", "a");
			grammarEntry.Add("guid", secondPosBeforeTest.Guid.ToString());
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosAfterTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest));
			Assert.That(secondPosAfterTest, Is.Not.Null);
			Assert.That(secondPosAfterTest.Guid, Is.EqualTo(secondPosBeforeTest.Guid));
			Assert.That(secondPosAfterTest, Is.SameAs(secondPosBeforeTest));
			Assert.That(secondPosAfterTest.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo("v"));
			Assert.That(secondPosAfterTest.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo("a"));
			// LF key shouldn't be copied to LCM, so don't test that one
		}

		[Test]
		public void Action_WithOneItemInMongoGrammarThatHasNoGuidAndIsNotWellKnown_ShouldAddOneNewItemInLcmGrammar()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosBeforeTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "k2");
			grammarEntry.Add("value", "v2");
			grammarEntry.Add("abbreviation", "a2");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosAfterTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			IPartOfSpeech newlyCreatedPos = cache.LangProject.AllPartsOfSpeech.FirstOrDefault(pos =>
				pos.Abbreviation.BestAnalysisVernacularAlternative.Text == "k2" && // NOTE: k2 not a2
				pos.Name.BestAnalysisVernacularAlternative.Text == "v2"
			);
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest + 1));
			Assert.That(secondPosAfterTest, Is.Not.Null);
			Assert.That(secondPosAfterTest.Guid, Is.EqualTo(secondPosBeforeTest.Guid));
			Assert.That(secondPosAfterTest, Is.SameAs(secondPosBeforeTest));
			Assert.That(secondPosAfterTest.Name.BestAnalysisVernacularAlternative.Text, Is.Not.EqualTo("v2"));
			Assert.That(secondPosAfterTest.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.Not.EqualTo("a2"));
			Assert.That(secondPosAfterTest.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.Not.EqualTo("k2"));
			Assert.That(newlyCreatedPos, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid, Is.Not.Null);
			Assert.That(newlyCreatedPos.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo("v2"));
			Assert.That(newlyCreatedPos.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo("k2"));
			// The newly-created part of speech will get its abbreviation from the LF key, not the LF abbrev.
			// TODO: Consider whether or not that's a bug, and whether it should use the (user-supplied) abbrev.
			// OTOH, they should be the same... unless LF has a non-English UI language. In which case we *need*
			// the English abbrev (the "key") and we *want* the non-English abbrev.
		}


		[Test]
		public void Action_WithOneWellKnownItemAndCorrectGuidInMongoGrammar_ShouldGetCorrectWellKnownGuidInLcm()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			string expectedGuid = PartOfSpeechMasterList.FlatPosGuidsFromAbbrevs["subordconn"];
			grammarEntry.Add("guid", expectedGuid);
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			string expectedName = PartOfSpeechMasterList.FlatPosNames[expectedGuid];
			string expectedAbbrev = PartOfSpeechMasterList.FlatPosAbbrevs[expectedGuid];
			IPartOfSpeech newlyCreatedPos = cache.LangProject.AllPartsOfSpeech.FirstOrDefault(pos =>
				pos.Name.BestAnalysisVernacularAlternative.Text == expectedName
			);
			Assert.That(newlyCreatedPos, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid.ToString(), Is.EqualTo(expectedGuid));
			Assert.That(newlyCreatedPos.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedName));
			Assert.That(newlyCreatedPos.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedAbbrev));
		}

		[Test]
		public void Action_WithOneWellKnownItemButNoGuidInMongoGrammar_ShouldGetCorrectWellKnownGuidInLcm()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			string expectedGuid = PartOfSpeechMasterList.FlatPosGuidsFromAbbrevs["subordconn"];
			string expectedName = PartOfSpeechMasterList.FlatPosNames[expectedGuid];
			string expectedAbbrev = PartOfSpeechMasterList.FlatPosAbbrevs[expectedGuid];
			IPartOfSpeech newlyCreatedPos = cache.LangProject.AllPartsOfSpeech.FirstOrDefault(pos =>
				pos.Name.BestAnalysisVernacularAlternative.Text == expectedName
			);
			Assert.That(newlyCreatedPos, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid.ToString(), Is.EqualTo(expectedGuid));
			Assert.That(newlyCreatedPos.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedName));
			Assert.That(newlyCreatedPos.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedAbbrev));
		}


		[Test]
		public void Action_WithOneWellKnownTopLevelItemButNoGuidInMongoGrammar_ShouldAddOnlyOneNewGrammarEntry()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "adp"); // Standard abbreviation for "adposition", a top-level entry
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest + 1));
		}

		[Test]
		public void Action_WithOneWellKnownItemThatHasOneParentButNoGuidInMongoGrammar_ShouldAddTwoNewGrammarEntries()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector", whose parent is "connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest + 2));
		}

		[Test]
		public void Action_WithOneWellKnownItemThatHasOneParentButNoGuidInMongoGrammar_ShouldAddTwoGrammarEntriesWithCorrectNamesAndParents()
		{
			// Setup
			var lfProj = _lfProj;
			LcmCache cache = lfProj.FieldWorksProject.Cache;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector", whose parent is "connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			SutMongoToLcm.Run(lfProj);

			// Verify
			char ORC = '\ufffc';
			string expectedGuid = PartOfSpeechMasterList.FlatPosGuidsFromAbbrevs["subordconn"];
			string[] expectedNames = PartOfSpeechMasterList.HierarchicalPosNames[expectedGuid].Split(ORC);
			string[] expectedAbbrevs = PartOfSpeechMasterList.HierarchicalPosAbbrevs[expectedGuid].Split(ORC);
			string expectedName = expectedNames[1];
			string expectedAbbrev = expectedAbbrevs[1];
			string expectedParentName = expectedNames[0];
			string expectedParentAbbrev = expectedAbbrevs[0];
			string expectedParentGuid = PartOfSpeechMasterList.FlatPosGuidsFromAbbrevs[expectedParentAbbrev];

			IPartOfSpeech newlyCreatedPos = cache.LangProject.AllPartsOfSpeech.FirstOrDefault(pos =>
				pos.Name.BestAnalysisVernacularAlternative.Text == expectedName
			);
			Assert.That(newlyCreatedPos, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid, Is.Not.Null);
			Assert.That(newlyCreatedPos.Guid.ToString(), Is.EqualTo(expectedGuid));
			Assert.That(newlyCreatedPos.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedName));
			Assert.That(newlyCreatedPos.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedAbbrev));
			Assert.That(newlyCreatedPos.OwningPossibility, Is.Not.Null);
			Assert.That(newlyCreatedPos.OwningPossibility, Is.InstanceOf<IPartOfSpeech>());
			Assert.That(newlyCreatedPos.OwningPossibility.Guid, Is.Not.Null);
			Assert.That(newlyCreatedPos.OwningPossibility.Guid.ToString(), Is.EqualTo(expectedParentGuid));
			Assert.That(newlyCreatedPos.OwningPossibility.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedParentName));
			Assert.That(newlyCreatedPos.OwningPossibility.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo(expectedParentAbbrev));
		}
		#endif
	}
}

