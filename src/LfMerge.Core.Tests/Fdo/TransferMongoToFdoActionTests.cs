// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Tests;
using LfMerge.Core.Tests.Fdo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LfMerge.Core.Tests.Fdo
{
	public class TransferMongoToFdoActionTests : FdoTestBase
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
			sutMongoToFdo.Run(lfProj);

			// Verify
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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

			// Initial FDO project has 63 entries, 3 internal pictures, and 1 externally linked picture
			FdoCache cache = _cache;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int originalNumOfFdoPictures = entryRepo.AllInstances().
				Count(e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfFdoEntries));
			Assert.That(originalNumOfFdoPictures, Is.EqualTo(3+1));
			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);
			var entryBefore = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.That(entryBefore.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entryBefore.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));

			// Exercise adding 1 picture with 2 captions. Note that the picture that was previously attached
			// to this FDO entry will end up being deleted, because it does not have a corresponding picture in LF.
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			sutMongoToFdo.Run(lfProj);

			// Verify "Added" picture is now the only picture on the sense (because the "old" picture was deleted),
			// and that it has 2 captions with the expected values.
			entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int numOfFdoPictures = entryRepo.AllInstances().
				Count(e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfFdoEntries));
			Assert.That(numOfFdoPictures, Is.EqualTo(originalNumOfFdoPictures));

			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));
			Assert.That(entry.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entry.SensesOS.First().PicturesOS.Count, Is.EqualTo(2));
			Assert.That(entry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo(expectedInternalFileName));
			Assert.That(entry.SensesOS[0].PicturesOS[1].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo(expectedExternalFileName));

			LfMultiText expectedNewCaption = ConvertFdoToMongoLexicon.
				ToMultiText(entry.SensesOS[0].PicturesOS[0].Caption, cache.ServiceLocator.WritingSystemManager);
			int expectedNumOfNewCaptions = expectedNewCaption.Count();
			Assert.That(expectedNumOfNewCaptions, Is.EqualTo(2));
			string expectedNewVernacularCaption = expectedNewCaption["qaa-x-kal"].Value;
			string expectedNewAnalysisCaption = expectedNewCaption["en"].Value;
			Assert.That(expectedNewVernacularCaption.Equals("First Vernacular caption"));
			Assert.That(expectedNewAnalysisCaption.Equals("Internal path reference"));

			var testSubEntry = cache.ServiceLocator.GetObject(Guid.Parse(TestSubEntryGuidStr)) as ILexEntry;
			Assert.That(testSubEntry, Is.Not.Null);
			Assert.That(testSubEntry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo("Pictures\\TestImage.tif"));
			var kenEntry = cache.ServiceLocator.GetObject(Guid.Parse(KenEntryGuidStr)) as ILexEntry;
			Assert.That(kenEntry, Is.Not.Null);
			Assert.That(kenEntry.SensesOS[0].PicturesOS[0].PictureFileRA.InternalPath.ToString(),
				Is.EqualTo("F:\\src\\xForge\\web-languageforge\\test\\php\\common\\TestImage.jpg"));
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

			// Initial FDO project has 63 entries, 3 internal pictures, and 1 externally linked picture
			FdoCache cache = _cache;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			int originalNumOfFdoPictures = entryRepo.AllInstances().Count(
				e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfFdoEntries));
			Assert.That(originalNumOfFdoPictures, Is.EqualTo(3+1));
			string expectedGuidStrBefore = data.bsonTestData["guid"].AsString;
			Guid expectedGuidBefore = Guid.Parse(expectedGuidStrBefore);
			var entryBefore = entryRepo.GetObject(expectedGuidBefore);
			Assert.That(entryBefore.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entryBefore.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));

			// Exercise running Action twice
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			sutMongoToFdo.Run(lfProj);
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			sutMongoToFdo.Run(lfProj);

			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);
			var entry = entryRepo.GetObject(expectedGuid);
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));

			Assert.That(entry.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entry.SensesOS.First().PicturesOS.Count, Is.EqualTo(2));
		}

		[Test]
		public void Action_WithEmptyMongoGrammar_ShouldPreserveFdoGrammarEntries()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosBeforeTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);
			Guid kenGuid = Guid.Parse(KenEntryGuidStr);
			entry = _conn.GetLfLexEntryByGuid(kenGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(1));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry added"));

			// Exercise again
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(1));
			Assert.That(_counts.Deleted,  Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry modified"));

			// Exercise again
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

			// Verify
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));
			Assert.That(_counts.Deleted,  Is.EqualTo(1));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge: 1 entry deleted"));

			// Exercise again
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = DateTime.UtcNow;
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string vernacularWS = cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string changedLexeme = "modified lexeme for this test";
			entry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, changedLexeme);
			entry.AuthorInfo = new LfAuthorInfo();
			entry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			sutMongoToFdo.Run(lfProj);

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
			sutFdoToMongo.Run(lfProj);

			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			LfLexEntry entry = _conn.GetLfLexEntryByGuid(entryGuid);
			entry.IsDeleted = true;
			_conn.UpdateMockLfLexEntry(entry);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			sutMongoToFdo.Run(lfProj);

			// Verify second run
			Assert.That(_counts.Deleted,  Is.EqualTo(0));
			// Added and Modified shouldn't have changed either, but check Deleted first
			// since that's the main point of this test
			Assert.That(_counts.Added,    Is.EqualTo(0));
			Assert.That(_counts.Modified, Is.EqualTo(0));

			Assert.That(LfMergeBridgeServices.FormatCommitMessageForLfMerge(_counts.Added, _counts.Modified, _counts.Deleted),
				Is.EqualTo("Language Forge S/R"));
		}

		#if false  // We've changed how we handle OptionLists since these tests were written, and they are no longer valid
		[Test]
		public void Action_WithOneItemInMongoGrammar_ShouldUpdateThatOneItemInFdoGrammar()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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
			sutMongoToFdo.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			IPartOfSpeech secondPosAfterTest = cache.LangProject.AllPartsOfSpeech.Skip(1).FirstOrDefault();
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest));
			Assert.That(secondPosAfterTest, Is.Not.Null);
			Assert.That(secondPosAfterTest.Guid, Is.EqualTo(secondPosBeforeTest.Guid));
			Assert.That(secondPosAfterTest, Is.SameAs(secondPosBeforeTest));
			Assert.That(secondPosAfterTest.Name.BestAnalysisVernacularAlternative.Text, Is.EqualTo("v"));
			Assert.That(secondPosAfterTest.Abbreviation.BestAnalysisVernacularAlternative.Text, Is.EqualTo("a"));
			// LF key shouldn't be copied to FDO, so don't test that one
		}

		[Test]
		public void Action_WithOneItemInMongoGrammarThatHasNoGuidAndIsNotWellKnown_ShouldAddOneNewItemInFdoGrammar()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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
			sutMongoToFdo.Run(lfProj);

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
		public void Action_WithOneWellKnownItemAndCorrectGuidInMongoGrammar_ShouldGetCorrectWellKnownGuidInFdo()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
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
			sutMongoToFdo.Run(lfProj);

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
		public void Action_WithOneWellKnownItemButNoGuidInMongoGrammar_ShouldGetCorrectWellKnownGuidInFdo()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "adp"); // Standard abbreviation for "adposition", a top-level entry
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			sutMongoToFdo.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest + 1));
		}

		[Test]
		public void Action_WithOneWellKnownItemThatHasOneParentButNoGuidInMongoGrammar_ShouldAddTwoNewGrammarEntries()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			int grammarCountBeforeTest = cache.LangProject.AllPartsOfSpeech.Count;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector", whose parent is "connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			sutMongoToFdo.Run(lfProj);

			// Verify
			int grammarCountAfterTest = cache.LangProject.AllPartsOfSpeech.Count;
			Assert.That(grammarCountAfterTest, Is.EqualTo(grammarCountBeforeTest + 2));
		}

		[Test]
		public void Action_WithOneWellKnownItemThatHasOneParentButNoGuidInMongoGrammar_ShouldAddTwoGrammarEntriesWithCorrectNamesAndParents()
		{
			// Setup
			var lfProj = _lfProj;
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			var data = new SampleData();
			BsonDocument grammarEntry = new BsonDocument();
			grammarEntry.Add("key", "subordconn"); // Standard abbreviation for "subordinating connector", whose parent is "connector"
			grammarEntry.Add("value", "NotTheRightName");
			grammarEntry.Add("abbreviation", "NotTheRightAbbrev");
			data.bsonOptionListData["items"] = new BsonArray(new BsonDocument[] { grammarEntry });

			_conn.UpdateMockOptionList(data.bsonOptionListData);

			// Exercise
			sutMongoToFdo.Run(lfProj);

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

