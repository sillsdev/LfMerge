// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;

namespace LfMerge.Core
{
	public static class MagicStrings
	{
		static MagicStrings()
		{
			MinimalModelVersion = 7000068;
		}

		public static Dictionary<string, string> LcmOptionlistNames = new Dictionary<string, string>()
		{
			// Option lists that are currently used in LF (as of 2016-03-01)
			{ LfOptionListCodeForGrammaticalInfo, "Part of Speech" },
			{ LfOptionListCodeForSemanticDomains, "Semantic Domain" },
			{ LfOptionListCodeForAcademicDomainTypes, "Academic Domains" },
			{ LfOptionListCodeForEnvironments, "Environments" },
			{ LfOptionListCodeForLocations, "Location" },
			{ LfOptionListCodeForUsageTypes, "Usages" },
			{ LfOptionListCodeForReversalTypes, "Reversal Entries" },
			{ LfOptionListCodeForSenseTypes, "Type" },
			{ LfOptionListCodeForAnthropologyCodes, "Anthropology Categories" },
			{ LfOptionListCodeForStatus, "Status" },

			// Option lists found in FW, but not currently used in LF (as of 2016-03-01)
			{ LfOptionListCodeForEtymology, "Etymology" },
			{ LfOptionListCodeForLexicalRelations, "Lexical Relation" },
			{ LfOptionListCodeForNoteTypes, "Note Type" },
			{ LfOptionListCodeForParadigms, "Paradigm" },
			{ LfOptionListCodeForUsers, "Users" },
			{ LfOptionListCodeForTranslationTypes, "Translation Type" },
			{ LfOptionListCodeForFromPartsOfSpeech, "From Part of Speech" },
			{ LfOptionListCodeForMorphTypes, "Morph Type" },
			{ LfOptionListCodeForNounSlots, "Noun Slot" },
			{ LfOptionListCodeForVerbSlots, "Verb Slot" },
			{ LfOptionListCodeForStativeSlots, "Stative Slot" },
			{ LfOptionListCodeForNounInflectionClasses, "Noun Inflection Class" },
			{ LfOptionListCodeForVerbInflectionClasses, "Verb Inflection Class" }
		};

		// Option lists that are currently used in LF (as of 2016-03-01)
		public const string LfOptionListCodeForGrammaticalInfo = "grammatical-info";
		public const string LfOptionListCodeForSemanticDomains = "semantic-domain-ddp4";
		public const string LfOptionListCodeForAcademicDomainTypes = "domain-type";
		public const string LfOptionListCodeForEnvironments = "environments";
		public const string LfOptionListCodeForLocations = "location";
		public const string LfOptionListCodeForUsageTypes = "usage-type";
		public const string LfOptionListCodeForReversalTypes = "reversal-type";
		public const string LfOptionListCodeForSenseTypes = "sense-type";
		public const string LfOptionListCodeForAnthropologyCodes = "anthro-code";
		public const string LfOptionListCodeForStatus = "status";

		// Option lists found in FW, but not currently used in LF (as of 2016-03-01)
		public const string LfOptionListCodeForEtymology = "etymology";
		public const string LfOptionListCodeForLexicalRelations = "lexical-relation";
		public const string LfOptionListCodeForNoteTypes = "note-type";
		public const string LfOptionListCodeForParadigms = "paradigm";
		public const string LfOptionListCodeForUsers = "users";
		public const string LfOptionListCodeForTranslationTypes = "translation-type";
		public const string LfOptionListCodeForFromPartsOfSpeech = "from-part-of-speech";
		public const string LfOptionListCodeForMorphTypes = "morph-type";
		public const string LfOptionListCodeForNounSlots = "noun-slot";
		public const string LfOptionListCodeForVerbSlots = "verb-slot";
		public const string LfOptionListCodeForStativeSlots = "stative-slot";
		public const string LfOptionListCodeForNounInflectionClasses = "noun-infl-class";
		public const string LfOptionListCodeForVerbInflectionClasses = "verb-infl-class";

		// Collections found in individual project DBs
		public const string LfCollectionNameForLexicon = "lexicon";
		public const string LfCollectionNameForLexiconComments = "lexiconComments";
		public const string LfCollectionNameForOptionLists = "optionlists";

		// Collections found in main DB
		public const string LfCollectionNameForProjectRecords = "projects";

		// Prefixes that LF wants for custom field names
		public const string LfCustomFieldEntryPrefix = "customField_entry";
		public const string LfCustomFieldSensesPrefix = "customField_senses";
		public const string LfCustomFieldExamplePrefix = "customField_example";

		// Field names to use in an LfCommentRegarding instance
		public const string LfFieldNameForDefinition = "definition";
		public const string LfFieldNameForExampleSentence = "definition";

		// Fake language codes used in storing custom GenDate and int fields in Mongo
		public const string LanguageCodeForGenDateFields = "qaa-Qaad";
		public const string LanguageCodeForIntFields = "qaa-Zmth";

		// FW strings
		public const string FwFixitAppName = "FixFwData.exe";

		// Other magic strings that don't fall into any particular category
		public const string UnknownString = "***";

		// Minimal supported model version (static property to support testing)
		public static int MinimalModelVersion { get; private set; }

		// Allow to set minimal model version during unit testing
		public static void SetMinimalModelVersion(int minimalModelVersion)
		{
			MinimalModelVersion = minimalModelVersion;
		}
	}
}

