// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoOptionList
	{
		protected int _wsForKeys;
		protected LfOptionList _lfOptionList;
		protected Dictionary<Guid, LfOptionListItem> _lfOptionListItemByGuid;
		protected Dictionary<string, LfOptionListItem> _lfOptionListItemByStrKey;
		protected Dictionary<Guid, string> _lfOptionListItemKeyByGuid;
		protected ILogger _logger;

		public ConvertFdoToMongoOptionList(LfOptionList lfOptionList, int wsForKeys, string listCode, ILogger logger)
		{
			_logger = logger;
			_wsForKeys = wsForKeys;
			if (lfOptionList == null)
				lfOptionList = MakeEmptyOptionList(listCode);
			_lfOptionList = lfOptionList;
			UpdateOptionListItemDictionaries(_lfOptionList);
		}

		public static string FdoOptionListName(string listCode) {
			if (MagicStrings.FdoOptionlistNames.ContainsKey(listCode)) {
				return MagicStrings.FdoOptionlistNames[listCode];
			}
			return listCode;
		}

		public virtual LfOptionList PrepareOptionListUpdate(ICmPossibilityList fdoOptionList)
		{
			Dictionary<Guid, ICmPossibility> fdoOptionListByGuid = fdoOptionList.ReallyReallyAllPossibilities
				// .Where(poss => poss.Guid != null) // Not needed as ICmPossibility GUIDs are not nullable
				.ToDictionary(poss => poss.Guid);

			foreach (ICmPossibility poss in fdoOptionListByGuid.Values)
			{
				LfOptionListItem correspondingItem;
				if (_lfOptionListItemByGuid.TryGetValue(poss.Guid, out correspondingItem))
				{
					SetOptionListItemFromCmPossibility(correspondingItem, poss);
				}
				else
				{
					correspondingItem = CmPossibilityToOptionListItem(poss);
				}
				_lfOptionListItemByGuid[poss.Guid] = correspondingItem;
				_lfOptionListItemByStrKey[correspondingItem.Key] = correspondingItem;
			}

			var lfNewOptionList = CloneOptionListWithEmptyItems(_lfOptionList);
			// We filter by "Does FDO have an option list item with corresponding GUID?" because if it doesn't,
			// then that option list item was probably deleted in FDO, so we should delete it here.
			lfNewOptionList.Items = _lfOptionListItemByGuid.Values
				.Where(item => fdoOptionListByGuid.ContainsKey(item.Guid.GetValueOrDefault()))
				.ToList();

			return lfNewOptionList;
		}

		public string LfItemKeyString(ICmPossibility fdoOptionListItem, int ws)
		{
			string result;
			if (fdoOptionListItem == null)
				return null;

			if (_lfOptionList != null)
			{
				if (_lfOptionListItemKeyByGuid.TryGetValue(fdoOptionListItem.Guid, out result))
					return result;

				// We shouldn't get here, because the option list SHOULD be pre-populated.
				_logger.Error("Got an option list item without a corresponding LF option list item. " +
					"In option list name '{0}', list code '{1}': " +
					"FDO option list item '{2}' had GUID {3} but no LF option list item was found",
					_lfOptionList.Name, _lfOptionList.Code,
					fdoOptionListItem.AbbrAndName, fdoOptionListItem.Guid
				);
				return null;
			}

			if (fdoOptionListItem.Abbreviation == null || fdoOptionListItem.Abbreviation.get_String(ws) == null)
			{
				// Last-ditch effort
				char ORC = '\ufffc';
				return fdoOptionListItem.AbbrevHierarchyString.Split(ORC).LastOrDefault();
			}
			else
			{
				return ConvertFdoToMongoTsStrings.SafeTsStringText(fdoOptionListItem.Abbreviation.get_String(ws));
			}
		}

		// For multi-option lists; use like "LfStringArrayField.FromStrings(_converter.LfItemKeyStrings(PossibilityList), _wsEn)".
		public IEnumerable<string> LfItemKeyStrings(IEnumerable<ICmPossibility> fdoOptionListItems, int ws)
		{
			foreach (ICmPossibility fdoOptionListItem in fdoOptionListItems)
				yield return LfItemKeyString(fdoOptionListItem, ws);
		}

		protected LfOptionListItem CmPossibilityToOptionListItem(ICmPossibility pos)
		{
			var item = new LfOptionListItem();
			SetOptionListItemFromCmPossibility(item, pos, true);
			return item;
		}

		protected void UpdateOptionListItemDictionaries(LfOptionList lfOptionList)
		{
			_lfOptionListItemByGuid = lfOptionList.Items
				.Where(item => item.Guid != null)
				.ToDictionary(item => item.Guid.Value, item => item);
			_lfOptionListItemByStrKey = lfOptionList.Items
				.ToDictionary(item => item.Key, item => item);
			_lfOptionListItemKeyByGuid = _lfOptionList.Items
				.Where(item => item.Guid != null)
				.ToDictionary(
					item => item.Guid.GetValueOrDefault(),
					item => item.Key
				);
		}

		protected LfOptionList MakeEmptyOptionList(string listCode)
		{
			var result = new LfOptionList();
			result.Items = new List<LfOptionListItem>();
			result.DateCreated = result.DateModified = DateTime.Now;
			result.Code = listCode;
			result.Name = FdoOptionListName(listCode);
			result.CanDelete = false;
			result.DefaultItemKey = null;
			return result;
		}

		// Clone old option list into new list, changing only the items
		// ... We could use a MongoDB update query for this, but that would
		// require new code in MongoConnection and MongoConnectionDouble.
		// TODO: We pretty much have that code by now. See if we can get rid of this function by now.
		protected LfOptionList CloneOptionListWithEmptyItems(LfOptionList original)
		{
			var newList = new LfOptionList();
			newList.CanDelete = original.CanDelete;
			newList.Code = original.Code;
			newList.DateCreated = original.DateCreated;
			newList.DateModified = DateTime.Now;
			newList.DefaultItemKey = original.DefaultItemKey;
			newList.Name = original.Name;
			// lfNewOptionList.Items is set to an empty list by its constructor; no need to set it here.
			return newList;
		}

		protected string FindAppropriateKey(string originalKey)
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

		protected void SetOptionListItemFromCmPossibility(LfOptionListItem item, ICmPossibility poss, bool setKey = false)
		{
			//const char ORC = '\xfffc';
			item.Abbreviation = ConvertFdoToMongoTsStrings.SafeTsStringText(poss.Abbreviation.BestAnalysisVernacularAlternative);
			if (setKey)
				item.Key = FindAppropriateKey(ConvertFdoToMongoTsStrings.SafeTsStringText(poss.Abbreviation.get_String(_wsForKeys)));
			item.Value = ConvertFdoToMongoTsStrings.SafeTsStringText(poss.Name.BestAnalysisVernacularAlternative);
			item.Guid = poss.Guid;
		}

	}
}
