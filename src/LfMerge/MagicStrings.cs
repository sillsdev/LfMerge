// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge
{
	public static class MagicStrings
	{
		public static Dictionary<string, string> FdoOptionlistNames = new Dictionary<string, string>()
		{
			{ "grammatical-info" , "Part of Speech" },
			{ "semantic-domain-ddp4" , "Semantic Domain" },
			{ "domain-type" , "Academic Domains" },
			{ "environments" , "Environments" },
			{ "location" , "Location" },
			{ "usage-type" , "Usages" },
			{ "reversal-type" , "Reversal Entries" },
			{ "sense-type" , "Type" },
			{ "anthro-code" , "Anthropology Categories" },
			{ "do-not-publish-in" , "Publish In" },
			{ "status" , "Status" },

			{ "etymology" , "Etymology" },
			{ "lexical-relation" , "Lexical Relation" },
			{ "note-type" , "Note Type" },
			{ "paradigm" , "Paradigm" },
			{ "users" , "Users" },
			{ "translation-type" , "Translation Type" },
			{ "from-part-of-speech" , "From Part of Speech" },
			{ "morph-type" , "Morph Type" },
			{ "noun-slot" , "Noun Slot" },
			{ "verb-slot" , "Verb Slot" },
			{ "stative-slot" , "Stative Slot" },
			{ "noun-infl-class" , "Noun Inflection Class" },
			{ "verb-infl-class" , "Verb Inflection Class" }
		};

		public const string LfOptionListCodeForGrammaticalInfo = "grammatical-info";
		public const string LfOptionListCodeForSenseTypes = "sense-type";

		// Collections found in individual project DBs
		public const string LfCollectionNameForLexicon = "lexicon";
		public const string LfCollectionNameForOptionLists = "optionlists";

		// Collections found in main DB
		public const string LfCollectionNameForProjectRecords = "projects";

		public const string UnknownString = "***";
		public const string FDOModelVersion = "7000068";
	}
}

