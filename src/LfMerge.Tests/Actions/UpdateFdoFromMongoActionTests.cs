// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using System;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	public class UpdateFdoFromMongoActionTests
	{
		public const string testProjectCode = "TestLangProj";
		private TestEnvironment _env;
		private MongoConnectionDouble _conn;
		private MongoProjectRecordFactory _recordFactory;
		private UpdateFdoFromMongoDbAction sut;

		[SetUp]
		public void Setup()
		{
			//_env = new TestEnvironment();
			_env = new TestEnvironment(testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("Fdo->Mongo tests need a mock MongoConnection in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Fdo->Mongo tests need a mock MongoProjectRecordFactory in order to work.");
			// TODO: If creating our own Mocks would be better than getting them from Autofac, do that instead.

			sut = new UpdateFdoFromMongoDbAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		// TODO: Need to switch this test over to a Mongo double that keeps data, so it can handle grammar info
		[Test]
		public void Action_Should_UpdateDefinitions()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var data = new SampleData();
			string newDefinition = "New definition for this unit test";
			data.bsonTestData["senses"][0]["definition"]["en"]["value"] = newDefinition;

			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, data.bsonTestData);

			// Exercise
			sut.Run(lfProj);

			// Verify
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			string expectedGuidStr = data.bsonTestData["guid"].AsString;
			string expectedShortName = data.bsonTestData["citationForm"].AsBsonDocument.GetElement(0).Value["value"].AsString;
			Guid expectedGuid = Guid.Parse(expectedGuidStr);

			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));
			Assert.That(entry.ShortName, Is.EqualTo(expectedShortName));
			Assert.That(entry.SensesOS[0].DefinitionOrGloss.BestAnalysisAlternative.Text, Is.EqualTo(newDefinition));
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
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

			_conn.AddToMockData<LfOptionList>(MagicStrings.LfCollectionNameForOptionLists, data.bsonOptionListData);

			// Exercise
			sut.Run(lfProj);

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

