// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using NUnit.Framework;
using LfMerge;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.FDO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Tests.Fdo
{
	public class UpdateMongoFromFdoActionTests : FdoTestBase
	{
		private LfOptionList CreateLfGrammarWith(IEnumerable<LfOptionListItem> grammarItems)
		{
			var result = new LfOptionList();
			result.Code = MagicStrings.LfOptionListCodeForGrammaticalInfo;
			result.Name = MagicStrings.LfOptionListNameForGrammaticalInfo;
			result.DateCreated = result.DateModified = System.DateTime.UtcNow;
			result.CanDelete = false;
			result.DefaultItemKey = null;
			result.Items = grammarItems.ToList();
			return result;
		}

		private IEnumerable<LfOptionListItem> DefaultGrammarItems(int howMany)
		{
			foreach (string guidStr in PartOfSpeechMasterList.FlatPosNames.Keys.Take(howMany))
			{
				string name = PartOfSpeechMasterList.FlatPosNames[guidStr];
				string abbrev = PartOfSpeechMasterList.FlatPosAbbrevs[guidStr];
				yield return new LfOptionListItem {
					Guid = Guid.Parse(guidStr),
					Key = abbrev,
					Abbreviation = abbrev,
					Value = name,
				};
			}
		}

		[Test]
		public void Action_ShouldPopulateMongoInputSystems()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			UpdateMongoDbFromFdo.InitialClone = true;
			Dictionary<string, LfInputSystemRecord> lfWsList = _conn.GetInputSystems(lfProj);
			Assert.That(lfWsList.Count, Is.EqualTo(0));

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			const int expectedNumVernacularWS = 3;
			const int expectedNumAnalysisWS = 2;
			// TODO: Investigate why qaa-Zxxx-x-kal-audio is in CurrentVernacularWritingSystems, but somehow in
			// UpdateMongoDbFromFdo.FdoWsToLfWs() is not contained in _cache.LangProject.CurrentVernacularWritingSystems
			const string notVernacularWs = "qaa-Zxxx-x-kal-audio";

			lfWsList = _conn.GetInputSystems(lfProj);
			var languageProj = lfProj.FieldWorksProject.Cache.LangProject;

			foreach (var fdoVernacularWs in languageProj.CurrentVernacularWritingSystems)
			{
				if (fdoVernacularWs.LanguageTag != notVernacularWs)
					Assert.That(lfWsList[fdoVernacularWs.LanguageTag].VernacularWS);
			}
			Assert.That(languageProj.CurrentVernacularWritingSystems.Count, Is.EqualTo(expectedNumVernacularWS));

			foreach (var fdoAnalysisWs in languageProj.CurrentAnalysisWritingSystems)
				Assert.That(lfWsList[fdoAnalysisWs.LanguageTag].AnalysisWS);
			Assert.That(languageProj.CurrentAnalysisWritingSystems.Count, Is.EqualTo(expectedNumAnalysisWS));
		}

		[Test]
		public void Action_ShouldUpdateLexemes()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			string[] searchOrder = new string[] { "en", "fr" };
			string expectedLexeme = "zitʰɛstmen";
			string expectedGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";

			IEnumerable<LfLexEntry> receivedData = _conn.StoredLfLexEntries.Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			LfLexEntry entry = receivedData.FirstOrDefault(e => e.Guid.ToString() == expectedGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.Lexeme.BestString(searchOrder), Is.EqualTo(expectedLexeme));
		}

		[Test]
		public void Action_WithEmptyMongoGrammar_ShouldPopulateMongoGrammarFromFdoGrammar()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			LfOptionList lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Null);

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProj.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithGuids_ShouldReplaceItemsFromLfGrammarWithItemsFromFdoGrammar()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			int initialGrammarItemCount = 10;
			LfOptionList lfGrammar = CreateLfGrammarWith(DefaultGrammarItems(initialGrammarItemCount));
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProj.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithNoGuids_ShouldStillReplaceItemsFromLfGrammarWithItemsFromFdoGrammar()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			int initialGrammarItemCount = 10;
			LfOptionList lfGrammar = CreateLfGrammarWith(DefaultGrammarItems(initialGrammarItemCount));
			foreach (LfOptionListItem item in lfGrammar.Items)
			{
				item.Guid = null;
			}
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProj.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithMatchingGuids_ShouldBeUpdatedFromFdoGrammar()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			var converter = new GrammarConverter(cache, null);
			LfOptionList lfGrammar = converter.PrepareGrammarOptionListUpdate(cache.LanguageProject.PartsOfSpeechOA);
			LfOptionListItem itemForTest = lfGrammar.Items.First();
			Guid g = itemForTest.Guid.Value;
			itemForTest.Abbreviation = "Different abbreviation";
			itemForTest.Value = "Different name";
			itemForTest.Key = "Different key";
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProj);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProj.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
			itemForTest = lfGrammar.Items.FirstOrDefault(x => x.Guid == g);
			Assert.That(itemForTest, Is.Not.Null);
			Assert.That(itemForTest.Abbreviation, Is.Not.EqualTo("Different abbreviation"));
			Assert.That(itemForTest.Value, Is.Not.EqualTo("Different name"));
			Assert.That(itemForTest.Key, Is.EqualTo("Different key")); // NOTE: Is.EqualTo, because keys shouldn't be updated
		}
	}
}

