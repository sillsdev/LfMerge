// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using NUnit.Framework;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using SIL.FieldWorks.FDO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Core.Tests.Fdo
{
	public class TransferFdoToMongoActionTests : FdoTestBase
	{
		private LfOptionList CreateLfGrammarWith(IEnumerable<LfOptionListItem> grammarItems)
		{
			var result = new LfOptionList();
			result.Code = MagicStrings.LfOptionListCodeForGrammaticalInfo;
			result.Name = MagicStrings.FdoOptionlistNames[MagicStrings.LfOptionListCodeForGrammaticalInfo];
			result.DateCreated = result.DateModified = System.DateTime.Now;
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
		public void Action_IsInitialClone_ShouldPopulateMongoInputSystems()
		{
			// Setup
			var lfProject = _lfProj;
			lfProject.IsInitialClone = true;
			Dictionary<string, LfInputSystemRecord> lfWsList = _conn.GetInputSystems(lfProject);
			Assert.That(lfWsList.Count, Is.EqualTo(0));

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			const int expectedNumVernacularWS = 3;
			const int expectedNumAnalysisWS = 2;
			// TODO: Investigate why qaa-Zxxx-x-kal-audio is in CurrentVernacularWritingSystems, but somehow in
			// UpdateMongoDbFromFdo.FdoWsToLfWs() is not contained in _cache.LangProject.CurrentVernacularWritingSystems
			const string notVernacularWs = "qaa-Zxxx-x-kal-audio";

			lfWsList = _conn.GetInputSystems(lfProject);
			var languageProj = lfProject.FieldWorksProject.Cache.LangProject;

			foreach (var fdoVernacularWs in languageProj.CurrentVernacularWritingSystems)
			{
				if (fdoVernacularWs.Id != notVernacularWs)
					Assert.That(lfWsList[fdoVernacularWs.Id].VernacularWS);
			}
			Assert.That(languageProj.CurrentVernacularWritingSystems.Count, Is.EqualTo(expectedNumVernacularWS));

			foreach (var fdoAnalysisWs in languageProj.CurrentAnalysisWritingSystems)
				Assert.That(lfWsList[fdoAnalysisWs.Id].AnalysisWS);
			Assert.That(languageProj.CurrentAnalysisWritingSystems.Count, Is.EqualTo(expectedNumAnalysisWS));
		}

		[Test]
		public void Action_IsInitialClone_ShouldUpdateDates()
		{
			// Setup
			var lfProject = _lfProj;
			lfProject.IsInitialClone = true;

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);

			LfLexEntry entry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.DateCreated, Is.EqualTo(DateTime.UtcNow).Within(5).Seconds);
			Assert.That(entry.DateModified, Is.EqualTo(DateTime.UtcNow).Within(5).Seconds);
			Assert.That(entry.AuthorInfo.CreatedDate, Is.EqualTo(DateTime.Parse("2004-10-19 02:42:02.903")));
			Assert.That(entry.AuthorInfo.ModifiedDate, Is.EqualTo(DateTime.Parse("2016-02-25 03:51:29.404")));
		}

		[Test]
		public void Action_NoDataChanged_ShouldUpdateLexemes()
		{
			// Setup
			var lfProject = _lfProj;

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			string[] searchOrder = new string[] { "en", "fr" };
			string expectedLexeme = "zitʰɛstmen";
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			LfLexEntry entry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.Lexeme.BestString(searchOrder), Is.EqualTo(expectedLexeme));
		}

		[Test]
		public void Action_NoDataChanged_ShouldUpdatePictures()
		{
			// Setup
			var lfProject = _lfProj;
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			int originalNumPictures = receivedData.Count(e => ((e.Senses.Count > 0) && (e.Senses[0].Pictures.Count > 0)));
			Assert.That(originalNumPictures, Is.EqualTo(0));

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify LF project now contains the 4 FDO pictures (1 externally linked, 3 internal)
			receivedData = _conn.GetLfLexEntries();
			int newNumPictures = receivedData.Count(e => ((e.Senses.Count > 0) && (e.Senses[0].Pictures.Count > 0)));
			Assert.That(newNumPictures, Is.EqualTo(4));
			LfLexEntry subEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestSubEntryGuidStr);
			Assert.That(subEntry, Is.Not.Null);
			Assert.That(subEntry.Senses[0].Pictures[0].FileName, Is.EqualTo("TestImage.tif"));
			LfLexEntry kenEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == KenEntryGuidStr);
			Assert.That(kenEntry, Is.Not.Null);
			Assert.That(kenEntry.Senses[0].Pictures[0].FileName, Is.EqualTo("F:\\src\\xForge\\web-languageforge\\test\\php\\common\\TestImage.jpg"));
		}

		[Test]
		public void Action_NoDataChanged_ShouldUpdateCustomFieldConfig()
		{
			// Setup
			var lfProject = _lfProj;

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			var customFieldConfig = _conn.GetCustomFieldConfig(lfProject);
			Assert.That(customFieldConfig.Count, Is.EqualTo(6));
			Assert.That(customFieldConfig.ContainsKey("customField_entry_Cust_MultiPara"));
			Assert.That(customFieldConfig["customField_entry_Cust_MultiPara"].HideIfEmpty, Is.False);
			LfConfigMultiParagraph entryMultiPara = (LfConfigMultiParagraph)customFieldConfig["customField_entry_Cust_MultiPara"];
			Assert.That(entryMultiPara.Label, Is.EqualTo("Cust MultiPara"));
		}

		[Test]
		public void Action_WithEmptyMongoGrammar_ShouldPopulateMongoGrammarFromFdoGrammar()
		{
			// Setup
			var lfProject = _lfProj;
			LfOptionList lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Null);

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProject.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithGuids_ShouldReplaceItemsFromLfGrammarWithItemsFromFdoGrammar()
		{
			// Setup
			var lfProject = _lfProj;
			int initialGrammarItemCount = 10;
			LfOptionList lfGrammar = CreateLfGrammarWith(DefaultGrammarItems(initialGrammarItemCount));
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProject.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithNoGuids_ShouldStillReplaceItemsFromLfGrammarWithItemsFromFdoGrammar()
		{
			// Setup
			var lfProject = _lfProj;
			int initialGrammarItemCount = 10;
			LfOptionList lfGrammar = CreateLfGrammarWith(DefaultGrammarItems(initialGrammarItemCount));
			foreach (LfOptionListItem item in lfGrammar.Items)
			{
				item.Guid = null;
			}
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProject.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
		}

		[Test]
		public void Action_WithPreviousMongoGrammarWithMatchingGuids_ShouldBeUpdatedFromFdoGrammar()
		{
			// Setup
			var lfProject = _lfProj;
			FdoCache cache = lfProject.FieldWorksProject.Cache;
			int wsEn = cache.WritingSystemFactory.GetWsFromStr("en");
			var converter = new ConvertFdoToMongoOptionList(null, wsEn,
				MagicStrings.LfOptionListCodeForGrammaticalInfo, new LfMerge.Core.Logging.NullLogger(),
				_cache.WritingSystemFactory);
			LfOptionList lfGrammar = converter.PrepareOptionListUpdate(cache.LanguageProject.PartsOfSpeechOA);
			LfOptionListItem itemForTest = lfGrammar.Items.First();
			Guid g = itemForTest.Guid.Value;
			itemForTest.Abbreviation = "Different abbreviation";
			itemForTest.Value = "Different name";
			itemForTest.Key = "Different key";
			_conn.UpdateMockOptionList(lfGrammar);

			// Exercise
			sutFdoToMongo.Run(lfProject);

			// Verify
			lfGrammar = _conn.GetLfOptionLists()
				.FirstOrDefault(optionList => optionList.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
			Assert.That(lfGrammar, Is.Not.Null);
			Assert.That(lfGrammar.Items, Is.Not.Empty);
			Assert.That(lfGrammar.Items.Count, Is.EqualTo(lfProject.FieldWorksProject.Cache.LanguageProject.AllPartsOfSpeech.Count));
			itemForTest = lfGrammar.Items.FirstOrDefault(x => x.Guid == g);
			Assert.That(itemForTest, Is.Not.Null);
			Assert.That(itemForTest.Abbreviation, Is.Not.EqualTo("Different abbreviation"));
			Assert.That(itemForTest.Value, Is.Not.EqualTo("Different name"));
			Assert.That(itemForTest.Key, Is.EqualTo("Different key")); // NOTE: Is.EqualTo, because keys shouldn't be updated
		}
	}
}

