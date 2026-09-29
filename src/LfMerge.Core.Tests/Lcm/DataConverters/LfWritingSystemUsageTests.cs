// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Model;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// Counting the text a writing system actually holds, field by field. This is a pure function
	/// over the Language Forge model, so the fixture needs neither Mongo nor an LCM project. It
	/// must not call MongoConnectionDouble.Initialize, which throws once another fixture in this
	/// assembly has registered the conventions.
	/// </summary>
	public class LfWritingSystemUsageTests
	{
		private static LfMultiText Text(params string[] tagThenValue)
		{
			var result = new LfMultiText();
			for (int i = 0; i < tagThenValue.Length; i += 2)
				result[tagThenValue[i]] = new LfStringField { Value = tagThenValue[i + 1] };
			return result;
		}

		private static LfLexEntry Entry(LfMultiText lexeme, params LfSense[] senses)
		{
			return new LfLexEntry { Lexeme = lexeme, Senses = new List<LfSense>(senses) };
		}

		private static LfSense Sense(LfMultiText gloss, params LfExample[] examples)
		{
			return new LfSense { Gloss = gloss, Examples = new List<LfExample>(examples) };
		}

		[Test]
		public void FromLexicon_CountsOneEntryPerNonEmptyAlternative()
		{
			var lexicon = new[] {
				Entry(Text("qaa-x-kal", "first", "en", "one")),
				Entry(Text("qaa-x-kal", "second")),
			};

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.EntriesCounted, Is.EqualTo(2));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "qaa-x-kal"), Is.EqualTo(2));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "en"), Is.EqualTo(1));
		}

		[Test]
		public void FromLexicon_DoesNotCountEmptyOrWhitespaceOrMissingText()
		{
			// A field Language Forge offers but nobody has filled in is exactly what the counts are
			// meant to tell apart from a field that is really in use.
			var lexicon = new[] {
				Entry(Text("qaa-x-kal", "", "en", "   ", "fr", "real")),
				new LfLexEntry { Lexeme = null },
			};

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.EntriesCounted, Is.EqualTo(2));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "qaa-x-kal"), Is.EqualTo(0));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "en"), Is.EqualTo(0));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "fr"), Is.EqualTo(1));
		}

		[Test]
		public void FromLexicon_SkipsDeletedEntries()
		{
			// Deleted entries are tombstones kept so the deletion can be replayed; their text is
			// not evidence that anyone is using that writing system.
			var lexicon = new[] {
				Entry(Text("qaa-x-kal", "kept")),
				new LfLexEntry { Lexeme = Text("qaa-x-kal", "gone"), IsDeleted = true },
			};

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.EntriesCounted, Is.EqualTo(1));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "qaa-x-kal"), Is.EqualTo(1));
		}

		[Test]
		public void FromLexicon_CountsNestedSenseAndExampleFields()
		{
			var lexicon = new[] {
				Entry(Text("kal", "word"),
					Sense(Text("en", "meaning"),
						new LfExample { Sentence = Text("kal", "a sentence"), Translation = Text("en", "a translation") },
						new LfExample { Sentence = Text("kal", "another") })),
			};

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Gloss, "en"), Is.EqualTo(1));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.ExampleSentence, "kal"), Is.EqualTo(2));
			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.ExampleTranslation, "en"), Is.EqualTo(1));
		}

		[Test]
		public void NonEmptyCount_SumsOverSeveralFields()
		{
			var lexicon = new[] {
				Entry(Text("kal", "word"),
					Sense(null, new LfExample { Sentence = Text("kal", "a sentence") })),
			};

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.NonEmptyCount(
				new[] { LfWritingSystemUsage.Lexeme, LfWritingSystemUsage.ExampleSentence }, "kal"),
				Is.EqualTo(2));
		}

		[Test]
		public void NonEmptyCount_MatchesTagsCaseInsensitively()
		{
			// Every LCM writing system lookup is case-insensitive, and the config and the lexicon do
			// not always agree on the case of a tag.
			var lexicon = new[] { Entry(Text("qaa-x-IPA-dupl1", "text")) };

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.NonEmptyCount(LfWritingSystemUsage.Lexeme, "qaa-x-ipa-dupl1"), Is.EqualTo(1));
		}

		[Test]
		public void FromLexicon_ToleratesAnEmptyOrNullLexicon()
		{
			Assert.That(LfWritingSystemUsage.FromLexicon(null).EntriesCounted, Is.EqualTo(0));
			Assert.That(LfWritingSystemUsage.FromLexicon(new LfLexEntry[0]).EntriesCounted, Is.EqualTo(0));
			Assert.That(LfWritingSystemUsage.FromLexicon(new LfLexEntry[0])
				.NonEmptyCount(LfWritingSystemUsage.Lexeme, "en"), Is.EqualTo(0));
		}

		[Test]
		public void TagsWithText_ListsOnlyTheWritingSystemsThatHoldText()
		{
			var lexicon = new[] { Entry(Text("kal", "word", "en", "", "fr", "mot")) };

			LfWritingSystemUsage usage = LfWritingSystemUsage.FromLexicon(lexicon);

			Assert.That(usage.TagsWithText(LfWritingSystemUsage.Lexeme), Is.EquivalentTo(new[] { "kal", "fr" }));
			Assert.That(usage.TagsWithText(LfWritingSystemUsage.Gloss), Is.Empty);
		}
	}
}
