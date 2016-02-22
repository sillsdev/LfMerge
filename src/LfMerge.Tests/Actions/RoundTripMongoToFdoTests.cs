// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	[TestFixture, Explicit, Category("LongRunning")]
	public class RoundTripMongoToFdoTests : RoundTripBase
	{
		private IDictionary<string, Tuple<string, string>> GetMongoDifferences(
			BsonDocument itemBeforeTest,
			BsonDocument itemAfterTest
		)
		{
			var fieldNamesThatShouldBeDifferent = new string[] {
				"DateCreated",
				"DateModified",
			};
			var differencesByName = new Dictionary<string, Tuple<string, string>>(); // Tuple of (before, after)
			foreach (var field in itemAfterTest)
			{
				if (fieldNamesThatShouldBeDifferent.Contains(field.Name))
					continue;

				if (!itemBeforeTest.Contains(field.Name))
				{
					differencesByName[field.Name] = new Tuple<string, string>(
						null,
						field.Value.ToString()
					);
				} 
				else if (field.Value != itemBeforeTest[field.Name])
				{
					differencesByName[field.Name] = new Tuple<string, string>(
						itemBeforeTest[field.Name].ToString(),
						field.Value.ToString()
					);
				}
			}

			return differencesByName;
		}
		
		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepOriginalValuesInEntries()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			sutFdoToMongo.Run(lfProject);
			IEnumerable<object> originalData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			LfLexEntry originalEntry = originalData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(61));
			Assert.That(originalEntry, Is.Not.Null);

			// Exercise
			sutMongoToFdo.Run(lfProject);
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(61));

			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(entry, Is.Not.Null);

			IDictionary<string, Tuple<string, string>> differencesByName = 
				GetMongoDifferences(originalEntry.ToBsonDocument(), entry.ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepModifiedValuesInEntries()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			sutFdoToMongo.Run(lfProject);
			IEnumerable<object> originalData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			LfLexEntry originalEntry = originalData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);

			string originalLexeme = originalEntry.Lexeme["qaa-x-kal"].Value;
			string changedLexeme = "Changed lexeme for this test";
			originalEntry.Lexeme["qaa-x-kal"].Value = changedLexeme;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);

			// Exercise
			sutMongoToFdo.Run(lfProject);
			string changedLexemeDuringUpdate = "This value should be overwritten by FdoToMongo";
			originalEntry.Lexeme["qaa-x-kal"].Value = changedLexemeDuringUpdate;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(61));

			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.Lexeme["qaa-x-kal"].Value, Is.Not.EqualTo(changedLexemeDuringUpdate));
			Assert.That(entry.Lexeme["qaa-x-kal"].Value, Is.EqualTo(changedLexeme));

			originalEntry.Lexeme["qaa-x-kal"].Value = originalLexeme;
			IDictionary<string, Tuple<string, string>> differencesByName = 
				GetMongoDifferences(originalEntry.ToBsonDocument(), entry.ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepModifiedValuesInSenses()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			sutFdoToMongo.Run(lfProject);
			IEnumerable<object> originalData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			LfLexEntry originalEntry = originalData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));

			string originalSense0Definition = originalEntry.Senses[0].Definition["en"].Value;
			string originalSense1Definition = originalEntry.Senses[1].Definition["en"].Value;
			string changedSense0Definition = "Changed sense0 definition for this test";
			string changedSense1Definition = "Changed sense1 definition for this test";
			originalEntry.Senses[0].Definition["en"].Value = changedSense0Definition;
			originalEntry.Senses[1].Definition["en"].Value = changedSense1Definition;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);

			// Exercise
			sutMongoToFdo.Run(lfProject);
			string changedDefinitionDuringUpdate = "This value should be overwritten by FdoToMongo";
			originalEntry.Senses[0].Definition["en"].Value = changedDefinitionDuringUpdate;
			originalEntry.Senses[1].Definition["en"].Value = changedDefinitionDuringUpdate;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(61));

			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(entry.Senses[0].Definition["en"].Value, Is.Not.EqualTo(changedDefinitionDuringUpdate));
			Assert.That(entry.Senses[1].Definition["en"].Value, Is.Not.EqualTo(changedDefinitionDuringUpdate));
			Assert.That(entry.Senses[0].Definition["en"].Value, Is.EqualTo(changedSense0Definition));
			Assert.That(entry.Senses[1].Definition["en"].Value, Is.EqualTo(changedSense1Definition));

			originalEntry.Senses[0].Definition["en"].Value = originalSense0Definition;
			originalEntry.Senses[1].Definition["en"].Value = originalSense1Definition;
			IDictionary<string, Tuple<string, string>> differencesByName = 
				GetMongoDifferences(originalEntry.Senses[0].ToBsonDocument(), entry.Senses[0].ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
			differencesByName = GetMongoDifferences(originalEntry.Senses[1].ToBsonDocument(), entry.Senses[1].ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
			differencesByName = GetMongoDifferences(originalEntry.ToBsonDocument(), entry.ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepModifiedValuesInExampleTranslations()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			sutFdoToMongo.Run(lfProject);
			IEnumerable<object> originalData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			LfLexEntry originalEntry = originalData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(originalEntry.Senses[0].Examples.Count, Is.EqualTo(2));

			string originalSense0Example0Translation = originalEntry.Senses[0].Examples[0].Translation["en"].Value;
			string originalSense0Example1Translation = originalEntry.Senses[0].Examples[1].Translation["en"].Value;
			string changedSense0Example0Translation = "Changed sense0 example0 sentence for this test";
			string changedSense0Example1Translation = "Changed sense0 example1 sentence for this test";
			originalEntry.Senses[0].Examples[0].Translation["en"].Value = changedSense0Example0Translation;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = changedSense0Example1Translation;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);

			// Exercise
			sutMongoToFdo.Run(lfProject);
			string changedTranslationDuringUpdate = "This value should be overwritten by FdoToMongo";
			originalEntry.Senses[0].Examples[0].Translation["en"].Value = changedTranslationDuringUpdate;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = changedTranslationDuringUpdate;
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, originalEntry);
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(61));

			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == testEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(entry.Senses[0].Examples[0].Translation["en"].Value, Is.Not.EqualTo(changedTranslationDuringUpdate));
			Assert.That(entry.Senses[0].Examples[1].Translation["en"].Value, Is.Not.EqualTo(changedTranslationDuringUpdate));
			Assert.That(entry.Senses[0].Examples[0].Translation["en"].Value, Is.EqualTo(changedSense0Example0Translation));
			Assert.That(entry.Senses[0].Examples[1].Translation["en"].Value, Is.EqualTo(changedSense0Example1Translation));

			originalEntry.Senses[0].Examples[0].Translation["en"].Value = originalSense0Example0Translation;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = originalSense0Example1Translation;
			IDictionary<string, Tuple<string, string>> differencesByName = 
				GetMongoDifferences(originalEntry.Senses[0].Examples[0].ToBsonDocument(), entry.Senses[0].Examples[0].ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
			differencesByName = GetMongoDifferences(originalEntry.Senses[0].Examples[1].ToBsonDocument(), entry.Senses[0].Examples[1].ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
			differencesByName = GetMongoDifferences(originalEntry.Senses[0].ToBsonDocument(), entry.Senses[0].ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
			differencesByName = GetMongoDifferences(originalEntry.ToBsonDocument(), entry.ToBsonDocument());
			Assert.That(differencesByName.Count(), Is.EqualTo(1));
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldAddNewEntry()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			sutFdoToMongo.Run(lfProject);
			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping("qaa-x-kal", newLexeme);
			_conn.AddToMockData<LfLexEntry>(MagicStrings.LfCollectionNameForLexicon, newEntry);
			string newEntryGuidStr = newEntry.Guid.ToString();

			IEnumerable<object> originalData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(62));

			// Exercise
			sutMongoToFdo.Run(lfProject);
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(62));

			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(entry, Is.Not.Null);

			IDictionary<string, Tuple<string, string>> differencesByName = 
				GetMongoDifferences(newEntry.ToBsonDocument(), entry.ToBsonDocument());
			// FDO-to-Mongo direction populates AuthorInfo and LiftID even if they were null in original,
			// so don't consider those two differences to be errors for this test.
			differencesByName.Remove("authorInfo");
			differencesByName.Remove("liftId");
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

	}
}
