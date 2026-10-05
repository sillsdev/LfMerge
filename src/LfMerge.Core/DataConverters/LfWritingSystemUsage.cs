// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using LfMerge.Core.LanguageForge.Model;

namespace LfMerge.Core.DataConverters
{
	/// <summary>
	/// How much text each writing system actually holds in each Language Forge field, counted over
	/// a project's lexicon.
	///
	/// The project config says which writing systems a field OFFERS, which is a much weaker claim
	/// than it looks: Language Forge routinely offers every input system in every field, so a tag
	/// configured for the lexeme field may well have no headword in it anywhere. Counting the text
	/// tells us what a writing system is really being used for, which is what
	/// ConvertMongoToLcmLexicon.ClassifyVernacularWritingSystems needs to tell a vernacular writing
	/// system from an analysis one.
	///
	/// Fields are keyed by the dotted path the config spells them with, nesting through "fields"
	/// ("senses.fields.examples.fields.sentence"), so the counts line up with the paths that
	/// classification already works in.
	/// </summary>
	public class LfWritingSystemUsage
	{
		// Field paths, spelled as the config spells them.
		public const string Lexeme = "lexeme";
		public const string CitationForm = "citationForm";
		public const string Definition = "senses.fields.definition";
		public const string Gloss = "senses.fields.gloss";
		public const string ExampleSentence = "senses.fields.examples.fields.sentence";
		public const string ExampleTranslation = "senses.fields.examples.fields.translation";
		public const string Note = "note";
		public const string LiteralMeaning = "literalMeaning";
		public const string Pronunciation = "pronunciation";

		private readonly Dictionary<string, Dictionary<string, int>> _countsByPath =
			new Dictionary<string, Dictionary<string, int>>();

		/// <summary>How many entries were counted; zero means we have no evidence to go on.</summary>
		public int EntriesCounted { get; private set; }

		/// <summary>The number of non-empty strings this writing system holds in this field.</summary>
		public int NonEmptyCount(string fieldPath, string tag)
		{
			Dictionary<string, int> byTag;
			int count;
			if (_countsByPath.TryGetValue(fieldPath, out byTag) && byTag.TryGetValue(tag, out count))
				return count;
			return 0;
		}

		/// <summary>
		/// The total number of non-empty strings this writing system holds across the given fields.
		/// </summary>
		public int NonEmptyCount(IEnumerable<string> fieldPaths, string tag)
		{
			int total = 0;
			foreach (string path in fieldPaths)
				total += NonEmptyCount(path, tag);
			return total;
		}

		/// <summary>The writing systems that hold any text in this field.</summary>
		public IEnumerable<string> TagsWithText(string fieldPath)
		{
			Dictionary<string, int> byTag;
			if (!_countsByPath.TryGetValue(fieldPath, out byTag))
				return new string[0];
			return byTag.Keys;
		}

		/// <summary>
		/// Counts the text in every built-in multitext field of every entry. Deleted entries are
		/// skipped: they are tombstones Language Forge keeps so the deletion can be replayed, and
		/// their text is not evidence of anything.
		/// </summary>
		public static LfWritingSystemUsage FromLexicon(IEnumerable<LfLexEntry> lexicon)
		{
			var usage = new LfWritingSystemUsage();
			if (lexicon == null)
				return usage;
			foreach (LfLexEntry entry in lexicon)
			{
				if (entry == null || entry.IsDeleted)
					continue;
				usage.EntriesCounted++;
				usage.Add(Lexeme, entry.Lexeme);
				usage.Add(CitationForm, entry.CitationForm);
				usage.Add(Note, entry.Note);
				usage.Add(LiteralMeaning, entry.LiteralMeaning);
				usage.Add("cvPattern", entry.CvPattern);
				usage.Add("entryBibliography", entry.EntryBibliography);
				usage.Add("entryRestrictions", entry.EntryRestrictions);
				usage.Add("etymology", entry.Etymology);
				usage.Add("etymologyComment", entry.EtymologyComment);
				usage.Add("etymologyGloss", entry.EtymologyGloss);
				usage.Add("etymologySource", entry.EtymologySource);
				usage.Add(Pronunciation, entry.Pronunciation);
				usage.Add("summaryDefinition", entry.SummaryDefinition);
				usage.Add("tone", entry.Tone);
				if (entry.Senses == null)
					continue;
				foreach (LfSense sense in entry.Senses)
				{
					if (sense == null)
						continue;
					usage.Add(Definition, sense.Definition);
					usage.Add(Gloss, sense.Gloss);
					usage.Add("senses.fields.anthropologyNote", sense.AnthropologyNote);
					usage.Add("senses.fields.discourseNote", sense.DiscourseNote);
					usage.Add("senses.fields.encyclopedicNote", sense.EncyclopedicNote);
					usage.Add("senses.fields.generalNote", sense.GeneralNote);
					usage.Add("senses.fields.grammarNote", sense.GrammarNote);
					usage.Add("senses.fields.phonologyNote", sense.PhonologyNote);
					usage.Add("senses.fields.scientificName", sense.ScientificName);
					usage.Add("senses.fields.semanticsNote", sense.SemanticsNote);
					usage.Add("senses.fields.senseBibliography", sense.SenseBibliography);
					usage.Add("senses.fields.senseImportResidue", sense.SenseImportResidue);
					usage.Add("senses.fields.senseRestrictions", sense.SenseRestrictions);
					usage.Add("senses.fields.sociolinguisticsNote", sense.SociolinguisticsNote);
					usage.Add("senses.fields.source", sense.Source);
					if (sense.Examples != null)
					{
						foreach (LfExample example in sense.Examples)
						{
							if (example == null)
								continue;
							usage.Add(ExampleSentence, example.Sentence);
							usage.Add(ExampleTranslation, example.Translation);
							usage.Add("senses.fields.examples.fields.reference", example.Reference);
						}
					}
					if (sense.Pictures != null)
					{
						foreach (LfPicture picture in sense.Pictures)
						{
							if (picture != null)
								usage.Add("senses.fields.pictures", picture.Caption);
						}
					}
				}
			}
			return usage;
		}

		/// <summary>
		/// Counts one field's non-empty alternatives. Whitespace counts as empty; span markup is
		/// not stripped first, since a string made only of markup carries no text to begin with.
		/// </summary>
		private void Add(string fieldPath, LfMultiText text)
		{
			if (text == null)
				return;
			foreach (KeyValuePair<string, LfStringField> kv in text)
			{
				if (kv.Value == null || string.IsNullOrWhiteSpace(kv.Value.Value))
					continue;
				Dictionary<string, int> byTag;
				if (!_countsByPath.TryGetValue(fieldPath, out byTag))
				{
					// Case-insensitively, as every LCM writing system lookup is: the config and the
					// lexicon do not always agree on the case of a tag, and they mean the same one.
					byTag = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
					_countsByPath[fieldPath] = byTag;
				}
				int count;
				byTag.TryGetValue(kv.Key, out count);
				byTag[kv.Key] = count + 1;
			}
		}
	}
}
