using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Config;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// LF has no vernacular/analysis flag on a writing system: which ones are vernacular is derived
	/// from the project's own config, not from languageCode. Configs are built by hand here, since
	/// this is a pure function needing no Mongo and no LCM project. The fixture must not call
	/// MongoConnectionDouble.Initialize -- it throws if another fixture in this assembly has already
	/// registered the conventions, and nothing here needs it.
	///
	/// Most cases are named after a real project in the 2026-07-06 corpus that has that shape.
	/// </summary>
	public class VernacularClassificationTests
	{
		private static LfConfigMultiText Field(params string[] inputSystems)
		{
			return new LfConfigMultiText { InputSystems = new List<string>(inputSystems) };
		}

		/// <summary>
		/// Builds a config from dotted paths spelled as Mongo spells them, so nesting goes through
		/// "fields" ("senses.fields.examples.fields.sentence").
		/// </summary>
		private static LfProjectConfig Config(params (string Path, LfConfigFieldBase Field)[] fields)
		{
			var entry = new LfConfigFieldList();
			var senses = new LfConfigFieldList();
			var examples = new LfConfigFieldList();
			foreach ((string path, LfConfigFieldBase field) in fields)
			{
				if (path.StartsWith("senses.fields.examples.fields."))
					examples.Fields[path.Substring("senses.fields.examples.fields.".Length)] = field;
				else if (path.StartsWith("senses.fields."))
					senses.Fields[path.Substring("senses.fields.".Length)] = field;
				else
					entry.Fields[path] = field;
			}
			if (examples.Fields.Count > 0) senses.Fields["examples"] = examples;
			if (senses.Fields.Count > 0) entry.Fields["senses"] = senses;
			return new LfProjectConfig { Entry = entry };
		}

		[Test]
		public void LexemeAndCitationFormAreAlwaysVernacular()
		{
			var config = Config(("lexeme", Field("seh")), ("citationForm", Field("seh-fonipa-x-etic")),
				("senses.fields.definition", Field("en", "pt")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "seh", "seh-fonipa-x-etic" }));
			Assert.That(result.Unresolved, Is.Empty);
		}

		[Test]
		public void UnionsLexemeAndCitationFormWithoutDuplicating()
		{
			var config = Config(
				("lexeme", Field("qaa-x-kal", "qaa-fonipa-x-kal")),
				("citationForm", Field("qaa-x-kal", "seh")),
				("senses.fields.definition", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal");
			Assert.That(result.Vernacular,
				Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal", "seh" }));
		}

		[Test]
		public void IgnoresTheAnalysisAnchorFields()
		{
			var config = Config(("lexeme", Field("qaa-x-kal")),
				("senses.fields.definition", Field("en", "fr")), ("senses.fields.gloss", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "qaa-x-kal" }));
			Assert.That(result.Vernacular, Does.Not.Contain("en"));
			Assert.That(result.Vernacular, Does.Not.Contain("fr"));
		}

		/// <summary>
		/// apltw2016, the case that started this: its lexeme field uses a phonetic writing system
		/// alongside the project language, and the rule this replaces -- vernacular is whatever
		/// equals languageCode -- filed that phonetic one as analysis.
		/// </summary>
		[Test]
		public void TreatsASecondLexemeWritingSystemAsVernacular()
		{
			var config = Config(("lexeme", Field("qaa-x-IPA-dupl1", "th")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "th");
			Assert.That(result.Vernacular, Contains.Item("th"));
			Assert.That(result.Vernacular, Contains.Item("qaa-x-IPA-dupl1"),
				"a writing system the lexeme field uses is vernacular even though it is not the language code");
		}

		[Test]
		public void MatchesTagsCaseInsensitively()
		{
			var config = Config(("lexeme", Field("qaa-x-IPA-dupl1")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "th");
			Assert.That(result.Vernacular.Contains("qaa-x-ipa-dupl1"), Is.True,
				"LCM's own writing system lookups are case-insensitive, so this should be too");
		}

		/// <summary>The 741-project shape: etymology written in the analysis language.</summary>
		[Test]
		public void EtymologyInAnAnalysisWritingSystemStaysAnalysis()
		{
			var config = Config(("lexeme", Field("yog")), ("senses.fields.gloss", Field("en")),
				("etymology", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "yog");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "yog" }));
			Assert.That(result.Vernacular, Does.Not.Contain("en"));
		}

		/// <summary>The 378-project shape, e.g. test-india-sena-01.</summary>
		[Test]
		public void EtymologyInTheVernacularIsVernacular()
		{
			var config = Config(("lexeme", Field("seh", "seh-fonipa-x-etic")),
				("senses.fields.gloss", Field("en", "pt")),
				("etymology", Field("seh", "seh-fonipa-x-etic")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "seh", "seh-fonipa-x-etic" }));
		}

		/// <summary>grc-vie-flex: etymologies in Hebrew and Aramaic, neither vernacular nor analysis.</summary>
		[Test]
		public void WritingSystemsInNeitherAnchorAreVernacularAndReported()
		{
			var config = Config(("lexeme", Field("grc")), ("senses.fields.gloss", Field("en", "vi")),
				("etymology", Field("hbo", "arc")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "grc");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "grc", "hbo", "arc" }));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "hbo", "arc" }));
		}

		/// <summary>
		/// With no analysis anchor there is nothing to rule a field out, so every other field's
		/// writing systems are unresolved -- and therefore vernacular.
		/// </summary>
		[Test]
		public void WithNoAnalysisAnchorEveryOtherFieldIsUnresolved()
		{
			var config = Config(("lexeme", Field("qaa-x-kal")), ("etymology", Field("qaa-fonipa-x-kal")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal" }));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "qaa-fonipa-x-kal" }));
		}

		/// <summary>flh-flex: languageCode names a writing system only etymology uses.</summary>
		[Test]
		public void LexemeWinsWhenLanguageCodeDisagrees()
		{
			var config = Config(("lexeme", Field("flh-x-cm")), ("senses.fields.gloss", Field("en", "id")),
				("etymology", Field("flh-x-ortho")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "flh-x-ortho");
			Assert.That(result.Vernacular, Contains.Item("flh-x-cm"));
		}

		/// <summary>odo: languageCode "th" is not among the project's writing systems at all.</summary>
		[Test]
		public void LexemeIsVernacularEvenWhenLanguageCodeIsAbsent()
		{
			var config = Config(("lexeme", Field("en")),
				("senses.fields.definition", Field("qaa-Syrc-IQ-x-syr", "en", "acm")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "th");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "en" }));
		}

		/// <summary>A writing system in both anchors is no evidence of any other field's role.</summary>
		[Test]
		public void AWritingSystemInBothAnchorsIsNotEvidence()
		{
			var config = Config(("lexeme", Field("en")), ("senses.fields.gloss", Field("en")),
				("etymology", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "en");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "en" }));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "en" }),
				"etymology overlaps neither anchor exclusively, so its role is unresolved");
		}

		/// <summary>
		/// The example sentence is nested, so this covers the recursion into a field list as well as
		/// the classification. Its sibling translation field is analysis and must stay that way.
		/// </summary>
		[Test]
		public void TheExampleSentenceIsClassifiedLikeAnyOtherField()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.definition", Field("fr")),
				("senses.fields.gloss", Field("en")),
				("senses.fields.examples.fields.sentence", Field("kal")),
				("senses.fields.examples.fields.translation", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "kal" }));
			Assert.That(result.Vernacular, Does.Not.Contain("en"),
				"the example translation is not a vernacular field");
			Assert.That(result.Unresolved, Is.Empty);
		}

		[Test]
		public void ToleratesAPartiallyMissingNestedPath()
		{
			// senses exists but has no children, so the walk has to give up cleanly.
			var config = Config(("lexeme", Field("qaa-x-kal")), ("senses", new LfConfigFieldList()));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "qaa-x-kal" }));
		}

		[Test]
		public void FallsBackToLanguageCodeWhenTheConfigSaysNothing()
		{
			Assert.That(ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(null, "fr").Vernacular,
				Is.EquivalentTo(new[] { "fr" }));
			Assert.That(ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(new LfProjectConfig(), "fr").Vernacular,
				Is.EquivalentTo(new[] { "fr" }));
		}

		[Test]
		public void FallsBackToLanguageCodeWhenLexemeListsNoInputSystems()
		{
			var config = Config(("lexeme", Field()));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "fr");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "fr" }));
		}

		[Test]
		public void ReturnsNothingWhenThereIsNoConfigAndNoLanguageCode()
		{
			Assert.That(ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(null, null).Vernacular,
				Is.Empty);
		}
	}
}
