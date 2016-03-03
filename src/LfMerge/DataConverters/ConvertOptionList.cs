// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.FDO;
using LfMerge.LanguageForge.Model;

namespace LfMerge.DataConverters
{
	public class ConvertOptionList
	{
		private static Dictionary<string, string> _fdoOptionlistNames = new Dictionary<string, string>()
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

		protected LfOptionList _lfOptionList;
		protected Dictionary<Guid, LfOptionListItem> _lfOptionListItemByGuid;
		protected Dictionary<string, LfOptionListItem> _lfOptionListItemByStrKey;

		public ConvertOptionList(LfOptionList lfOptionList)
		{
			if (lfOptionList == null)
				lfOptionList = MakeEmptyOptionList(MagicStrings.LfOptionListCodeForGrammaticalInfo);
			_lfOptionList = lfOptionList;
			UpdateOptionListItemDictionaries(_lfOptionList);
		}

		public static string FdoOptionListName(string listCode) {
			if (_fdoOptionlistNames.ContainsKey(listCode)) {
				return _fdoOptionlistNames[listCode];
			}
			return listCode;
		}

		public virtual LfOptionList PrepareOptionListUpdate(ICmPossibilityList fdoOptionList)
		{
			Dictionary<Guid, ICmPossibility> fdoOptionListByGuid = fdoOptionList.ReallyReallyAllPossibilities
				.OfType<ICmPossibility>()
				// .Where(pos => pos.Guid != null) // Not needed as ICmPossibility GUIDs are not nullable
				.ToDictionary(pos => pos.Guid, pos => pos);

			// Clone old option list into new list, changing only the items
			// ... We could use a MongoDB update query for this, but that would
			// require new code in MongoConnection and MongoConnectionDouble.
			// TODO: When appropriate, write that new code. Until then, we clone manually.
			var lfNewOptionList = new LfOptionList();
			lfNewOptionList.CanDelete = _lfOptionList.CanDelete;
			lfNewOptionList.Code = _lfOptionList.Code;
			lfNewOptionList.DateCreated = _lfOptionList.DateCreated;
			lfNewOptionList.DateModified = DateTime.UtcNow;
			lfNewOptionList.DefaultItemKey = _lfOptionList.DefaultItemKey;
			lfNewOptionList.Name = _lfOptionList.Name;
			// We filter by "Does FDO have a PoS with corresponding GUID?" because if it doesn't,
			// then that part of speech was probably deleted in FDO, so we should delete it here.
			lfNewOptionList.Items = _lfOptionListItemByGuid.Values
				.Where(item => fdoOptionListByGuid.ContainsKey(item.Guid.GetValueOrDefault()))
				.ToList();
			return lfNewOptionList;
		}

		protected void UpdateOptionListItemDictionaries(LfOptionList lfOptionList)
		{
			_lfOptionListItemByGuid = lfOptionList.Items
				.Where(item => item.Guid != null)
				.ToDictionary(item => item.Guid.Value, item => item);
			_lfOptionListItemByStrKey = lfOptionList.Items
				.ToDictionary(item => item.Key, item => item);
		}

		protected LfOptionList MakeEmptyOptionList(string listCode)
		{
			var result = new LfOptionList();
			result.Items = new List<LfOptionListItem>();
			result.DateCreated = result.DateModified = DateTime.UtcNow;
			result.Code = listCode;
			result.Name = FdoOptionListName(listCode);
			result.CanDelete = false;
			result.DefaultItemKey = null;
			return result;
		}

	}
}
