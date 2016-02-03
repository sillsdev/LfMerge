// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using LfMerge.LanguageForge.Model;

namespace LfMerge.DataConverters
{
	public class GrammarConverter
	{
		private FdoCache _cache;
		private LfOptionList _lfGrammar;
		private Dictionary<Guid, LfOptionListItem> _lfGrammarByGuid;
		private Dictionary<string, LfOptionListItem> _lfGrammarByStrKey;
		private int _wsForKeys;

		// TODO: We don't really need a cache, just a WritingSystemFactory, or even just an int to set wsForKeys from
		public GrammarConverter(FdoCache cache, LfOptionList lfGrammar)
		{
			_cache = cache;
			_wsForKeys = _cache.WritingSystemFactory.GetWsFromStr("en");
			if (lfGrammar == null)
				lfGrammar = MakeEmptyGrammarOptionList();
			_lfGrammar = lfGrammar;
			UpdateGrammarDicts(_lfGrammar);
		}

		private LfOptionList MakeEmptyGrammarOptionList()
		{
			var result = new LfOptionList();
			result.Items = new List<LfOptionListItem>();
			result.DateCreated = result.DateModified = DateTime.UtcNow;
			result.Code = MagicStrings.LfOptionListCodeForGrammaticalInfo;
			result.Name = MagicStrings.LfOptionListNameForGrammaticalInfo;
			result.CanDelete = false;
			result.DefaultItemKey = null;
			return result;
		}

		private void UpdateGrammarDicts(LfOptionList lfGrammar)
		{
			_lfGrammarByGuid = lfGrammar.Items
				.Where(item => item.Guid != null)
				.ToDictionary(item => item.Guid.Value, item => item);
			_lfGrammarByStrKey = lfGrammar.Items
				.ToDictionary(item => item.Key, item => item);
		}

		private string ToStringOrNull(ITsString iTsString)
		{
			if (iTsString == null) return null;
			return iTsString.Text;
		}

		private string AbbrevHierarchyStringForWs(ICmPossibility poss, int wsId)
		{
			// The CmPossibility.AbbrevHierarchyString property uses the default analysis language.
			// But we need to force a specific language (English) even if that is not the analysis language.
			string ORC = "\ufffc";
			ICmPossibility current = poss;
			LinkedList<ICmPossibility> allAncestors = new LinkedList<ICmPossibility>();
			while (current != null)
			{
				allAncestors.AddFirst(current);
				current = current.Owner as ICmPossibility;
			}
			// TODO: The below line might fail if one of them doesn't have a corresponding string. Deal with that case.
			return string.Join(ORC, allAncestors.Select(ancestor => ancestor.Abbreviation.get_String(wsId).Text));
		}

		private string FindAppropriateKey(string originalKey)
		{
			if (originalKey == null)
				originalKey = MagicStrings.UnknownString; // Can't let a null key exist, so use something non-representative
			string currentTry = originalKey;
			int extraNum = 0;
			while (_lfGrammarByStrKey.ContainsKey(currentTry))
			{
				extraNum++;
				currentTry = originalKey + extraNum.ToString();
			}
			return currentTry;
		}

		private void SetOptionListItemFromPartOfSpeech(LfOptionListItem item, IPartOfSpeech pos, bool setKey = false)
		{
			const char ORC = '\xfffc';
			item.Abbreviation = ToStringOrNull(pos.Abbreviation.BestAnalysisVernacularAlternative);
			if (setKey)
				item.Key = FindAppropriateKey(ToStringOrNull(pos.Abbreviation.get_String(_wsForKeys)));
			item.Value = ToStringOrNull(pos.Name.BestAnalysisVernacularAlternative);
			item.Guid = pos.Guid;
		}

		private LfOptionListItem PartOfSpeechToOptionListItem(IPartOfSpeech pos)
		{
			var item = new LfOptionListItem();
			SetOptionListItemFromPartOfSpeech(item, pos, true);
			return item;
		}

		public LfOptionList PrepareGrammarOptionListUpdate(ICmPossibilityList partsOfSpeech)
		{
			Dictionary<Guid, IPartOfSpeech> fdoGrammarByGuid = partsOfSpeech.ReallyReallyAllPossibilities
				.OfType<IPartOfSpeech>()
				// .Where(pos => pos.Guid != null) // Not needed as IPartOfSpeech GUIDs are not nullable
				.ToDictionary(pos => pos.Guid, pos => pos);

			foreach (IPartOfSpeech pos in fdoGrammarByGuid.Values)
			{
				LfOptionListItem correspondingItem;
				if (_lfGrammarByGuid.TryGetValue(pos.Guid, out correspondingItem))
				{
					SetOptionListItemFromPartOfSpeech(correspondingItem, pos);
				}
				else
				{
					correspondingItem = PartOfSpeechToOptionListItem(pos);
				}
				_lfGrammarByGuid.Add(pos.Guid, correspondingItem);
				_lfGrammarByStrKey.Add(correspondingItem.Key, correspondingItem);
			}

			// Clone old grammar list into new list, changing only the items
			// ... We could use a MongoDB update query for this, but that would
			// require new code in MongoConnection and MongoConnectionDouble.
			// TODO: When appropriate, write that new code. Until then, we clone manually.
			var newOptionList = new LfOptionList();
			newOptionList.CanDelete = _lfGrammar.CanDelete;
			newOptionList.Code = _lfGrammar.Code;
			newOptionList.DateCreated = _lfGrammar.DateCreated;
			newOptionList.DateModified = DateTime.UtcNow;
			newOptionList.DefaultItemKey = _lfGrammar.DefaultItemKey;
			newOptionList.Name = _lfGrammar.Name;
			// We filter by "Does FDO have a PoS with corresponding GUID?" because if it doesn't,
			// then that part of speech was probably deleted in FDO, so we should delete it here.
			newOptionList.Items = _lfGrammarByGuid.Values
				.Where(item => fdoGrammarByGuid.ContainsKey(item.Guid.GetValueOrDefault()))
				.ToList();
			return newOptionList;
		}
	}
}

