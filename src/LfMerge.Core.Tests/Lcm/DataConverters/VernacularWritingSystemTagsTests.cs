using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Config;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// LF has no vernacular/analysis flag on a writing system: which ones are vernacular is implied
	/// by the config fields that use them. These cover that derivation on its own, without an LCM
	/// project, since it is a pure function of the config and the project's language code.
	/// </summary>
	public class VernacularWritingSystemTagsTests
	{
		private static LfProjectConfig ConfigWith(params (string FieldName, string[] InputSystems)[] fields)
		{
			var entry = new LfConfigFieldList();
			foreach ((string fieldName, string[] inputSystems) in fields)
			{
				entry.Fields[fieldName] = new LfConfigMultiText {
					InputSystems = new List<string>(inputSystems)
				};
			}
			return new LfProjectConfig { Entry = entry };
		}

		[Test]
		public void TakesTheLexemeFieldsInputSystems()
		{
			var config = ConfigWith(("lexeme", new[] { "qaa-x-kal", "qaa-fonipa-x-kal" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal" }));
		}

		[Test]
		public void UnionsLexemeAndCitationFormWithoutDuplicating()
		{
			var config = ConfigWith(
				("lexeme", new[] { "qaa-x-kal", "qaa-fonipa-x-kal" }),
				("citationForm", new[] { "qaa-x-kal", "seh" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal", "seh" }));
		}

		[Test]
		public void TakesTheEtymologyFieldsInputSystems()
		{
			var config = ConfigWith(
				("lexeme", new[] { "qaa-x-kal" }),
				("etymology", new[] { "qaa-fonipa-x-kal" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal" }));
		}

		/// <summary>
		/// The example sentence lives at senses.fields.examples.fields.sentence, so this covers the
		/// nested path as well as the field itself.
		/// </summary>
		[Test]
		public void TakesTheExampleSentenceFieldsInputSystems()
		{
			var entry = new LfConfigFieldList();
			entry.Fields["lexeme"] = new LfConfigMultiText { InputSystems = new List<string> { "qaa-x-kal" } };
			var examples = new LfConfigFieldList();
			examples.Fields["sentence"] = new LfConfigMultiText { InputSystems = new List<string> { "seh" } };
			examples.Fields["translation"] = new LfConfigMultiText { InputSystems = new List<string> { "en" } };
			var senses = new LfConfigFieldList();
			senses.Fields["examples"] = examples;
			senses.Fields["definition"] = new LfConfigMultiText { InputSystems = new List<string> { "fr" } };
			entry.Fields["senses"] = senses;
			var config = new LfProjectConfig { Entry = entry };

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal", "seh" }));
			Assert.That(tags, Does.Not.Contain("en"), "the example translation is not a vernacular field");
			Assert.That(tags, Does.Not.Contain("fr"), "the sense definition is not a vernacular field");
		}

		[Test]
		public void ToleratesAPartiallyMissingNestedPath()
		{
			// senses exists but has no examples, so the nested lookup has to give up cleanly.
			var entry = new LfConfigFieldList();
			entry.Fields["lexeme"] = new LfConfigMultiText { InputSystems = new List<string> { "qaa-x-kal" } };
			entry.Fields["senses"] = new LfConfigFieldList();
			var config = new LfProjectConfig { Entry = entry };

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal" }));
		}

		[Test]
		public void IgnoresAnalysisFields()
		{
			var config = ConfigWith(
				("lexeme", new[] { "qaa-x-kal" }),
				("definition", new[] { "en", "fr" }),
				("gloss", new[] { "en" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "qaa-x-kal");

			Assert.That(tags, Is.EquivalentTo(new[] { "qaa-x-kal" }));
			Assert.That(tags, Does.Not.Contain("en"));
		}

		/// <summary>
		/// The case this change is for. apltw2016's lexeme field uses a phonetic writing system
		/// alongside the project language, and the old rule -- vernacular is whatever equals
		/// languageCode -- filed that phonetic one as analysis.
		/// </summary>
		[Test]
		public void TreatsASecondLexemeWritingSystemAsVernacular()
		{
			var config = ConfigWith(("lexeme", new[] { "qaa-x-IPA-dupl1", "th" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "th");

			Assert.That(tags, Contains.Item("th"));
			Assert.That(tags, Contains.Item("qaa-x-IPA-dupl1"),
				"a writing system the lexeme field uses is vernacular even though it is not the language code");
		}

		[Test]
		public void MatchesTagsCaseInsensitively()
		{
			var config = ConfigWith(("lexeme", new[] { "qaa-x-IPA-dupl1" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "th");

			Assert.That(tags.Contains("qaa-x-ipa-dupl1"), Is.True,
				"LCM's own writing system lookups are case-insensitive, so this should be too");
		}

		[TestCase(TestName = "FallsBackToLanguageCodeWhenNoVernacularFieldHasInputSystems")]
		public void FallsBackToLanguageCode()
		{
			// A config with only analysis fields: nothing says what the vernacular is, so the
			// project's language code is the best available answer -- the rule this replaced.
			var config = ConfigWith(("definition", new[] { "en" }));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "fr");

			Assert.That(tags, Is.EquivalentTo(new[] { "fr" }));
		}

		[Test]
		public void FallsBackToLanguageCodeWhenLexemeListsNoInputSystems()
		{
			var config = ConfigWith(("lexeme", new string[0]));

			var tags = ConvertMongoToLcmLexicon.VernacularWritingSystemTags(config, "fr");

			Assert.That(tags, Is.EquivalentTo(new[] { "fr" }));
		}

		[Test]
		public void ToleratesAMissingConfig()
		{
			Assert.That(ConvertMongoToLcmLexicon.VernacularWritingSystemTags(null, "fr"),
				Is.EquivalentTo(new[] { "fr" }));
			Assert.That(ConvertMongoToLcmLexicon.VernacularWritingSystemTags(new LfProjectConfig(), "fr"),
				Is.EquivalentTo(new[] { "fr" }));
		}

		[Test]
		public void ReturnsNothingWhenThereIsNoConfigAndNoLanguageCode()
		{
			Assert.That(ConvertMongoToLcmLexicon.VernacularWritingSystemTags(null, null), Is.Empty);
		}
	}
}
