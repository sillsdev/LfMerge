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
	public class GrammarConverter : ConvertOptionList
	{
		private int _wsForKeys;

		// TODO: We don't really need a cache, just a WritingSystemFactory, or even just an int to set wsForKeys from
		public GrammarConverter(FdoCache cache, LfOptionList lfOptionList) : base(lfOptionList)
		{
			_wsForKeys = cache.WritingSystemFactory.GetWsFromStr("en");
		}

		public override LfOptionList PrepareOptionListUpdate(ICmPossibilityList fdoOptionList)
		{
			Dictionary<Guid, IPartOfSpeech> fdoOptionListByGuid = fdoOptionList.ReallyReallyAllPossibilities
				.OfType<IPartOfSpeech>()
				// .Where(pos => pos.Guid != null) // Not needed as IPartOfSpeech GUIDs are not nullable
				.ToDictionary(pos => pos.Guid, pos => pos);

			foreach (IPartOfSpeech pos in fdoOptionListByGuid.Values)
			{
				LfOptionListItem correspondingItem;
				if (_lfOptionListItemByGuid.TryGetValue(pos.Guid, out correspondingItem))
				{
					SetOptionListItemFromPartOfSpeech(correspondingItem, pos);
				}
				else
				{
					correspondingItem = PartOfSpeechToOptionListItem(pos);
				}
				_lfOptionListItemByGuid[pos.Guid] = correspondingItem;
				_lfOptionListItemByStrKey[correspondingItem.Key] = correspondingItem;
			}

			return base.PrepareOptionListUpdate(fdoOptionList);
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
			while (_lfOptionListItemByStrKey.ContainsKey(currentTry))
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
	}
}
