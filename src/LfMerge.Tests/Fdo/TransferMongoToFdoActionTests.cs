// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using LfMerge.Tests.Fdo;
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
using System.Linq;

namespace LfMerge.Tests.Fdo
{
	public class TransferMongoToFdoActionTests : FdoTestBase
	{
		[Test]
		public void Action_Should_UpdateDefinitions()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var data = new SampleData();
			string newDefinition = "New definition for this unit test";
			data.bsonTestData["senses"][0]["definition"]["en"]["value"] = newDefinition;

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
		public void Action_Should_UpdatePictures()
		{
			// Setup initial Mongo project has 1 picture and 2 captions
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var data = new SampleData();
			int newMongoPictures = data.bsonTestData["senses"][0]["pictures"].AsBsonArray.Count;
			int newMongoCaptions = data.bsonTestData["senses"][0]["pictures"][0]["caption"].AsBsonDocument.Count();
			Assert.That(newMongoPictures, Is.EqualTo(1));
			Assert.That(newMongoCaptions, Is.EqualTo(2));

			// Initial FDO project has 63 entries, 3 internal pictures, and 1 externally linked picture
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			ILexEntryRepository entryRepo = cache.ServiceLocator.GetInstance<ILexEntryRepository>();
			int originalNumOfFdoPictures = entryRepo.AllInstances().Count(
				e => (e.SensesOS.Count > 0) && (e.SensesOS[0].PicturesOS.Count > 0));
			Assert.That(entryRepo.Count, Is.EqualTo(originalNumOfFdoEntries));
			Assert.That(originalNumOfFdoPictures, Is.EqualTo(3+1));
			string expectedGuidStrBefore = data.bsonTestData["guid"].AsString;
			Guid expectedGuidBefore = Guid.Parse(expectedGuidStrBefore);
			var entryBefore = cache.ServiceLocator.GetObject(expectedGuidBefore) as ILexEntry;
			Assert.That(entryBefore.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entryBefore.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));

			// Exercise adding 1 picture with 2 captions. Note that the picture that was previously attached
			// to this FDO entry will end up being deleted, because it does not have a corresponding picture in LF.
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			sutMongoToFdo.Run(lfProj);

			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);
			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));

			// Verify "Added" picture is now the only picture on the sense (because the "old" picture was deleted),
			// and that it has 2 captions with the expected values.
			Assert.That(entry.SensesOS.Count, Is.GreaterThan(0));
			Assert.That(entry.SensesOS.First().PicturesOS.Count, Is.EqualTo(1));
			LfMultiText expectedNewCaption = ConvertFdoToMongoLexicon.ToMultiText(
				entry.SensesOS[0].PicturesOS[0].Caption, cache.ServiceLocator.WritingSystemManager);
			int expectedNumOfNewCaptions = expectedNewCaption.Count();
			Assert.That(expectedNumOfNewCaptions, Is.EqualTo(2));

			string expectedNewVernacularCaption = expectedNewCaption["qaa-x-kal"].Value;
			string expectedNewAnalysisCaption = expectedNewCaption["en"].Value;
			Assert.That(expectedNewVernacularCaption.Equals("First Vernacular caption"));
			Assert.That(expectedNewAnalysisCaption.Equals("First Analysis caption"));
		}

		[Test]
		public void Action_WithEmptyMongoGrammar_ShouldPreserveFdoGrammarEntries()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
		public void Action_WithOneItemInMongoGrammar_ShouldUpdateThatOneItemInFdoGrammar()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
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
	}
}

