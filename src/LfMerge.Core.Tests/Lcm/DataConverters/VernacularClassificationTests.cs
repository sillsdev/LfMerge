using System;
using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
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

		/// <summary>
		/// One entry holding text at the given field path, so a lexicon of them gives a writing
		/// system a known amount of text in a field of known role.
		/// </summary>
		private static LfLexEntry EntryWithTextAt(string fieldPath, string tag)
		{
			var text = new LfMultiText { { tag, LfStringField.FromString("text") } };
			var entry = new LfLexEntry();
			switch (fieldPath)
			{
			case LfWritingSystemUsage.Lexeme: entry.Lexeme = text; break;
			case LfWritingSystemUsage.CitationForm: entry.CitationForm = text; break;
			case LfWritingSystemUsage.Definition:
				entry.Senses = new List<LfSense> { new LfSense { Definition = text } }; break;
			case LfWritingSystemUsage.Gloss:
				entry.Senses = new List<LfSense> { new LfSense { Gloss = text } }; break;
			case LfWritingSystemUsage.ExampleSentence:
				entry.Senses = new List<LfSense> {
					new LfSense { Examples = new List<LfExample> { new LfExample { Sentence = text } } } };
				break;
			case LfWritingSystemUsage.ExampleTranslation:
				entry.Senses = new List<LfSense> {
					new LfSense { Examples = new List<LfExample> { new LfExample { Translation = text } } } };
				break;
			case LfWritingSystemUsage.Note: entry.Note = text; break;
			case LfWritingSystemUsage.LiteralMeaning: entry.LiteralMeaning = text; break;
			case LfWritingSystemUsage.Pronunciation: entry.Pronunciation = text; break;
			case "etymology": entry.Etymology = text; break;
			default: throw new ArgumentException("no test entry shape for " + fieldPath);
			}
			return entry;
		}

		private static LfWritingSystemUsage Usage(params (string Path, string Tag, int Count)[] spec)
		{
			var lexicon = new List<LfLexEntry>();
			foreach ((string path, string tag, int count) in spec)
				for (int i = 0; i < count; i++)
					lexicon.Add(EntryWithTextAt(path, tag));
			return LfWritingSystemUsage.FromLexicon(lexicon);
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
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en" }));
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

		/// <summary>
		/// A vernacular field is evidence about the field, not about every writing system in it:
		/// Portuguese is exclusive to the analysis anchors, so an etymology that also carries the
		/// vernacular must not make it vernacular -- a new Portuguese writing system would then be
		/// created vernacular-only, and the glosses in it would have no analysis writing system.
		/// </summary>
		[Test]
		public void AVernacularFieldDoesNotMakeAnAnalysisOnlyWritingSystemVernacular()
		{
			var config = Config(("lexeme", Field("seh")), ("senses.fields.gloss", Field("en", "pt")),
				("etymology", Field("seh", "pt")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "seh" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en", "pt" }));
		}

		/// <summary>
		/// A field offering a vernacular-only and an analysis-only writing system side by side says
		/// nothing about the role of the others it offers. It used to count as vernacular, because
		/// that was checked first, which made kam-flex's Spanish and French vernacular; FLEx has
		/// both as analysis.
		/// </summary>
		[Test]
		public void AFieldOfferingBothRolesIsNotEvidenceOfEither()
		{
			var config = Config(("lexeme", Field("seh")), ("senses.fields.gloss", Field("en")),
				("customField_senses_Wordlist", Field("seh", "en", "fr")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh");
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "fr" }));
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "seh", "fr" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en", "fr" }));
		}

		/// <summary>
		/// grc-vie-flex: etymologies in Hebrew and Aramaic, neither vernacular nor analysis. With no
		/// evidence either way they are both, so neither a vernacular-typed nor an analysis-typed
		/// field is left without them.
		/// </summary>
		[Test]
		public void WritingSystemsInNeitherAnchorAreBothRolesAndReported()
		{
			var config = Config(("lexeme", Field("grc")), ("senses.fields.gloss", Field("en", "vi")),
				("etymology", Field("hbo", "arc")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "grc");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "grc", "hbo", "arc" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en", "vi", "hbo", "arc" }));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "hbo", "arc" }));
		}

		/// <summary>
		/// A French-only notes field overlaps neither anchor. French must reach the analysis list, or
		/// an analysis-typed FieldWorks custom field would not offer it.
		/// </summary>
		[Test]
		public void AnUnresolvedCustomFieldWritingSystemIsAnalysisToo()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("senses.fields.customField_senses_note", Field("fr")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");
			Assert.That(result.Analysis, Contains.Item("fr"));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "fr" }));
		}

		/// <summary>
		/// With no analysis anchor there is nothing to rule a field out, so every other field's
		/// writing systems are unresolved -- and therefore both vernacular and analysis. Unless
		/// their language has already been spoken for: qaa-fonipa-x-kal is a phonetic spelling of
		/// the same language as the lexeme's qaa-x-kal, so it is vernacular like its relative,
		/// while Hebrew keeps no such company and stays in doubt.
		/// </summary>
		[Test]
		public void WithNoAnalysisAnchorEveryOtherFieldIsUnresolved()
		{
			var config = Config(("lexeme", Field("qaa-x-kal")),
				("etymology", Field("qaa-fonipa-x-kal", "hbo")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal");
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "hbo" }));
			Assert.That(result.Vernacular,
				Is.EquivalentTo(new[] { "qaa-x-kal", "qaa-fonipa-x-kal", "hbo" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "hbo" }));
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

		/// <summary>
		/// A writing system in both anchors is no evidence of any other field's role, so the Hebrew
		/// sharing the etymology with it is in doubt. "en" itself is not: the anchors placed it.
		/// </summary>
		[Test]
		public void AWritingSystemInBothAnchorsIsNotEvidence()
		{
			var config = Config(("lexeme", Field("en")), ("senses.fields.gloss", Field("en")),
				("etymology", Field("en", "hbo")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "en");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "en", "hbo" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en", "hbo" }));
			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "hbo" }),
				"etymology overlaps neither anchor exclusively, so the writing system it alone places is in doubt");
		}

		/// <summary>
		/// The "en" in lexeme and gloss alike: it is vernacular because the lexeme uses it, and it is
		/// analysis because the gloss does. Being one must not stop it being the other.
		/// </summary>
		[Test]
		public void AWritingSystemInBothAnchorsIsBothVernacularAndAnalysis()
		{
			var config = Config(("lexeme", Field("kal", "en")), ("senses.fields.gloss", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "kal", "en" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en" }));
		}

		/// <summary>
		/// The example sentence and translation are anchors nested in a field list, so this covers
		/// the recursion into one: the sentence is vernacular and the translation analysis.
		/// </summary>
		[Test]
		public void TheNestedExampleAnchorsAreFound()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.definition", Field("fr")),
				("senses.fields.gloss", Field("en")),
				("senses.fields.examples.fields.sentence", Field("kal")),
				("senses.fields.examples.fields.translation", Field("en")));
			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");
			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "kal" }));
			Assert.That(result.Vernacular, Does.Not.Contain("en"),
				"the example translation is not a vernacular field");
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en", "fr" }));
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

		/// <summary>
		/// The case that motivates counting at all: the config offers "en" for the lexeme field, so
		/// the config alone calls it vernacular, but no headword is written in it.
		/// </summary>
		[Test]
		public void AWritingSystemConfiguredForTheLexemeButUnusedThereIsNotVernacular()
		{
			var config = Config(("lexeme", Field("kal", "en")), ("senses.fields.gloss", Field("en")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 100),
				(LfWritingSystemUsage.Gloss, "en", 100));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "kal" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en" }));
			Assert.That(result.Vernacular, Does.Not.Contain("en"),
				"nothing is written in en in the lexeme field, so it is no evidence of vernacular");
		}

		/// <summary>
		/// brb-flex-2022: the phonetic writing system has 4,030 headwords and one stray string in an
		/// example reference, and FieldWorks has it as vernacular only.
		/// </summary>
		[Test]
		public void AFewStrayStringsDoNotMakeAVernacularWritingSystemAnalysisAsWell()
		{
			var config = Config(("lexeme", Field("seh-fonipa-x-etic")), ("senses.fields.gloss", Field("en", "seh-fonipa-x-etic")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "seh-fonipa-x-etic", 4030),
				(LfWritingSystemUsage.Gloss, "seh-fonipa-x-etic", 1),
				(LfWritingSystemUsage.Gloss, "en", 4000));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh", usage);

			Assert.That(result.Vernacular, Contains.Item("seh-fonipa-x-etic"));
			Assert.That(result.Analysis, Does.Not.Contain("seh-fonipa-x-etic"));
			Assert.That(result.Analysis, Contains.Item("en"));
		}

		/// <summary>
		/// brb-flex-2022 again: the vernacular is also glossed in, 72/28, and FieldWorks really does
		/// list it in both.
		/// </summary>
		[Test]
		public void AWritingSystemUsedSubstantiallyForBothRolesGetsBoth()
		{
			var config = Config(("lexeme", Field("brb")), ("senses.fields.gloss", Field("brb", "en")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "brb", 7256),
				(LfWritingSystemUsage.Gloss, "brb", 2801));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "brb", usage);

			Assert.That(result.Vernacular, Contains.Item("brb"));
			Assert.That(result.Analysis, Contains.Item("brb"));
			Assert.That(result.Unresolved, Is.Empty, "28% is a real second role, not an unknown one");
		}

		[Test]
		public void TextOutweighsTheConfigInBothDirections()
		{
			// Configured the wrong way round: the lexeme offers "en" and the gloss offers "kal",
			// but every headword is in kal and every gloss in en.
			var config = Config(("lexeme", Field("en")), ("senses.fields.gloss", Field("kal")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 50),
				(LfWritingSystemUsage.Gloss, "en", 50));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "en", usage);

			Assert.That(result.Vernacular, Is.EquivalentTo(new[] { "kal" }));
			Assert.That(result.Analysis, Is.EquivalentTo(new[] { "en" }));
		}

		[Test]
		public void AWritingSystemUsedOnlyInAFieldOfUnknownRoleIsStillUnresolved()
		{
			// Text in the etymology says nothing: the corpus has 33 projects writing etymologies in
			// the vernacular and 50 in an analysis language.
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("etymology", Field("hbo")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), ("etymology", "hbo", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "hbo" }));
		}

		[Test]
		public void AWritingSystemTheConfigForgotIsClassifiedFromItsTextAnyway()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), (LfWritingSystemUsage.Lexeme, "kal-fonipa", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Vernacular, Contains.Item("kal-fonipa"));
		}

		[Test]
		public void AnEmptyLexiconLeavesTheConfigInCharge()
		{
			var config = Config(("lexeme", Field("kal", "en")), ("senses.fields.gloss", Field("en")));

			var withoutData = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal",
				LfWritingSystemUsage.FromLexicon(new LfLexEntry[0]));
			var configOnly = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");

			Assert.That(withoutData.Vernacular, Is.EquivalentTo(configOnly.Vernacular));
			Assert.That(withoutData.Analysis, Is.EquivalentTo(configOnly.Analysis));
			Assert.That(withoutData.Vernacular, Contains.Item("en"));
		}

		/// <summary>
		/// FieldWorks fixes the role of these fields, and the corpus agrees: counting the projects
		/// whose text in each is exclusively one role, the example sentence runs 332 vernacular to 5
		/// analysis, and the translation, entry note and literal meaning run the other way.
		/// </summary>
		[TestCase("senses.fields.examples.fields.sentence", true)]
		[TestCase("senses.fields.examples.fields.translation", false)]
		[TestCase("note", false)]
		[TestCase("literalMeaning", false)]
		public void TheFieldsFieldWorksFixesTheRoleOfAreEvidenceToo(string fieldPath, bool isVernacular)
		{
			// Only the field under test carries any text for "xyz", so nothing else can decide it.
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				(fieldPath, Field("xyz")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), (fieldPath, "xyz", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Does.Not.Contain("xyz"));
			if (isVernacular)
			{
				Assert.That(result.Vernacular, Contains.Item("xyz"));
				Assert.That(result.Analysis, Does.Not.Contain("xyz"));
			}
			else
			{
				Assert.That(result.Analysis, Contains.Item("xyz"));
				Assert.That(result.Vernacular, Does.Not.Contain("xyz"));
			}
		}

		/// <summary>
		/// The pronunciation field is not an anchor. It looks vernacular in the corpus at 114
		/// projects to 16, but 12% is above the share a single writing system may hold in the other
		/// role and still count as one thing, so projects evidently use it for more than one. Being
		/// offered there, with nothing written in it, therefore settles nothing.
		/// </summary>
		[Test]
		public void ThePronunciationFieldIsNotAnAnchor()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("pronunciation", Field("xyz")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Contains.Item("xyz"));
		}

		/// <summary>
		/// But text in it makes a writing system vernacular, since FieldWorks draws its
		/// pronunciation writing systems from the vernacular ones. xin-flex's "xin" holds 226
		/// pronunciations and nothing else.
		/// </summary>
		[Test]
		public void PronunciationTextMakesAWritingSystemVernacular()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("pronunciation", Field("xyz")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), (LfWritingSystemUsage.Pronunciation, "xyz", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Does.Not.Contain("xyz"));
			Assert.That(result.Vernacular, Contains.Item("xyz"));
			Assert.That(result.Analysis, Does.Not.Contain("xyz"));
		}

		/// <summary>
		/// A writing system offered only in a custom field, with pronunciation text and nothing
		/// else, is vernacular only, by design. Without the pronunciation text the custom field
		/// would say nothing about it and it would be both; with it, it counts as placed, so that
		/// field cannot put it in doubt. In the 2026-10-01 corpus every writing system in that
		/// position is a pronunciation writing system -- a phonetic, IPA or audio one -- for which
		/// vernacular only is the right answer.
		/// </summary>
		[Test]
		public void PronunciationTextAloneMakesAWritingSystemOfferedOnlyInACustomFieldVernacularOnly()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_senses_Phonetic", Field("kal-fonipa")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), (LfWritingSystemUsage.Pronunciation, "kal-fonipa", 1));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Vernacular, Contains.Item("kal-fonipa"));
			Assert.That(result.Analysis, Does.Not.Contain("kal-fonipa"));
			Assert.That(result.Unresolved, Does.Not.Contain("kal-fonipa"));
		}

		/// <summary>
		/// Pronunciation text only adds the vernacular role; it does not take away an analysis role
		/// the config gives. In dtlat-flex a single pronunciation string in Swedish would otherwise
		/// make Swedish, configured for the gloss, vernacular only.
		/// </summary>
		[Test]
		public void PronunciationTextDoesNotTakeAwayAnAnalysisRole()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en", "sv")),
				("pronunciation", Field("kal", "sv")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10), (LfWritingSystemUsage.Pronunciation, "sv", 1));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Vernacular, Contains.Item("sv"));
			Assert.That(result.Analysis, Contains.Item("sv"));
		}

		/// <summary>
		/// A writing system offered both in a field that says nothing about its role and in one
		/// whose company settles it is settled by the latter. The field of no role is listed first
		/// on purpose: placing as each field came, it used to put the writing system in doubt.
		/// </summary>
		[TestCase("kal", true)]
		[TestCase("en", false)]
		public void AWritingSystemInDoubtIsSettledByTheCompanyItKeeps(string companion, bool isVernacular)
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Doubtful", Field("abc", "xyz")),
				("customField_entry_Settled", Field(companion, "abc")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Does.Not.Contain("abc"),
				"abc shares a field with {0}, which is settled, so abc is settled too", companion);
			Assert.That(result.Vernacular.Contains("abc"), Is.EqualTo(isVernacular));
			Assert.That(result.Analysis.Contains("abc"), Is.EqualTo(!isVernacular));
		}

		/// <summary>
		/// The answer must not depend on the order the config lists its fields in. Here en-fonipa is
		/// offered in a vernacular field and an analysis field, so it plays both roles. Placing as
		/// each field came, a field of no role listed before them put it in doubt, and language
		/// affinity with "en" then stripped its vernacular role; listed after them, it did not.
		/// </summary>
		[TestCase(true, TestName = "TheFieldOrderDoesNotChangeTheAnswerWithTheNoRoleFieldFirst")]
		[TestCase(false, TestName = "TheFieldOrderDoesNotChangeTheAnswerWithTheNoRoleFieldLast")]
		public void TheFieldOrderDoesNotChangeTheAnswer(bool noRoleFieldFirst)
		{
			var noRole = ("customField_entry_NoRole", (LfConfigFieldBase)Field("en-fonipa"));
			var fields = new List<(string, LfConfigFieldBase)> {
				("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Vernacular", Field("kal", "en-fonipa")),
				("customField_entry_Analysis", Field("en", "en-fonipa")),
			};
			fields.Insert(noRoleFieldFirst ? 2 : fields.Count, noRole);
			var config = Config(fields.ToArray());

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal");

			Assert.That(result.Vernacular, Contains.Item("en-fonipa"));
			Assert.That(result.Analysis, Contains.Item("en-fonipa"));
			Assert.That(result.Unresolved, Does.Not.Contain("en-fonipa"));
		}

		[Test]
		public void WritingSystemsWhoseOnlyCompanyIsEachOtherStayUnresolved()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Doubtful", Field("abc", "xyz")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "abc", "xyz" }));
		}

		/// <summary>
		/// "en" is in both anchors, so it plays both roles. Turning up alone in a note, a field
		/// that gives no evidence, must not put it in doubt: if it did, the company it keeps in the
		/// lexeme field (the vernacular) would then strip it of its analysis role, and a new "en"
		/// writing system would be created vernacular only.
		/// </summary>
		[Test]
		public void AWritingSystemAlreadyPlacedIsNotPutInDoubtByAFieldWithNoEvidence()
		{
			var config = Config(("lexeme", Field("seh", "en")), ("senses.fields.gloss", Field("en")),
				("senses.fields.definition", Field("en")), ("senses.fields.generalNote", Field("en")));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh");

			Assert.That(result.Unresolved, Is.Empty);
			Assert.That(result.Vernacular, Contains.Item("en"));
			Assert.That(result.Analysis, Contains.Item("en"));
		}

		[Test]
		public void AWritingSystemWithNoCompanyAtAllStaysUnresolved()
		{
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Note", Field("xyz")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "xyz" }));
		}

		/// <summary>
		/// A writing system with nothing written in it and no company still has a language, and the
		/// other writing systems of that language have already been placed.
		/// </summary>
		[TestCase("seh", "seh-fonipa-x-etic", true, TestName = "AffinityPhoneticVariantOfTheVernacular")]
		[TestCase("qaa-x-kal", "qaa-Zxxx-x-kal-audio", true, TestName = "AffinityAudioVariantOfTheVernacular")]
		[TestCase("qaa-x-kal", "qaa-x-kal-dupl1", true, TestName = "AffinityDuplicateOfTheVernacular")]
		[TestCase("x-kal", "x-kal-dupl1", true, TestName = "AffinityDuplicateOfAWhollyPrivateUseVernacular")]
		public void AWritingSystemOfTheSameLanguageAsTheVernacularIsVernacular(
			string vernacularTag, string relative, bool isVernacular)
		{
			var config = Config(("lexeme", Field(vernacularTag)), ("senses.fields.gloss", Field("en")),
				("customField_entry_Extra", Field(relative)));
			var usage = Usage((LfWritingSystemUsage.Lexeme, vernacularTag, 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, vernacularTag, usage);

			Assert.That(result.Unresolved, Is.Empty);
			Assert.That(result.Vernacular.Contains(relative), Is.EqualTo(isVernacular));
			Assert.That(result.Analysis.Contains(relative), Is.EqualTo(!isVernacular));
		}

		[Test]
		public void AWritingSystemOfTheSameLanguageAsTheAnalysisLanguageIsAnalysis()
		{
			// Script and region are not the language: "en-GB" is English.
			var config = Config(("lexeme", Field("kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Extra", Field("en-GB")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "kal", usage);

			Assert.That(result.Unresolved, Is.Empty);
			Assert.That(result.Analysis, Contains.Item("en-GB"));
			Assert.That(result.Vernacular, Does.Not.Contain("en-GB"));
		}

		/// <summary>
		/// Under "qaa" the private-use subtags are the language, so two of them are two languages.
		/// </summary>
		[Test]
		public void ADifferentPrivateUseLanguageIsNotTheSameLanguage()
		{
			var config = Config(("lexeme", Field("qaa-x-kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Extra", Field("qaa-x-hbo")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "qaa-x-kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "qaa-x-kal", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "qaa-x-hbo" }));
		}

		/// <summary>
		/// A tag can be private-use from its first subtag (the corpus has "x-IPA" and "x-v"), and
		/// then too the private-use subtags are the language.
		/// </summary>
		[Test]
		public void AWhollyPrivateUseTagIsNotTheSameLanguageAsAnother()
		{
			var config = Config(("lexeme", Field("x-kal")), ("senses.fields.gloss", Field("en")),
				("customField_entry_Extra", Field("x-hbo")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "x-kal", 10),
				(LfWritingSystemUsage.Gloss, "en", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "x-kal", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "x-hbo" }));
		}

		[Test]
		public void ALanguageUsedForBothRolesSettlesNothing()
		{
			// seh is the vernacular and seh-fonipa is glossed in, so the language says nothing.
			var config = Config(("lexeme", Field("seh")), ("senses.fields.gloss", Field("seh-fonipa")),
				("customField_entry_Extra", Field("seh-Latn")));
			var usage = Usage((LfWritingSystemUsage.Lexeme, "seh", 10),
				(LfWritingSystemUsage.Gloss, "seh-fonipa", 10));

			var result = ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(config, "seh", usage);

			Assert.That(result.Unresolved, Is.EquivalentTo(new[] { "seh-Latn" }));
		}

		[Test]
		public void ReturnsNothingWhenThereIsNoConfigAndNoLanguageCode()
		{
			Assert.That(ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems(null, null).Vernacular,
				Is.Empty);
		}
	}
}
