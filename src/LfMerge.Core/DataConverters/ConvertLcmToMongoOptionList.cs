// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LfMerge.Core.DataConverters
{
	public class ConvertLcmToMongoOptionList
	{
		protected int _wsForKeys;
		protected LfOptionList _lfOptionList;
		protected Dictionary<Guid, LfOptionListItem> _lfOptionListItemByGuid;
		protected Dictionary<string, LfOptionListItem> _lfOptionListItemByStrKey;
		protected Dictionary<Guid, string> _lfOptionListItemKeyByGuid;
		protected ILogger _logger;
		protected ILgWritingSystemFactory _wsf;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConvertLcmToMongoOptionList"/> class.
		/// </summary>
		/// <param name="lfOptionList">Lf option list.</param>
		/// <param name="wsForKeys">Ws for keys.</param>
		/// <param name="listCode">List code.</param>
		/// <param name="logger">Logger.</param>
		public ConvertLcmToMongoOptionList(LfOptionList lfOptionList, int wsForKeys, string listCode, ILogger logger, ILgWritingSystemFactory wsf)
		{
			_logger = logger;
			_wsf = wsf;
			_wsForKeys = wsForKeys;
			if (lfOptionList == null)
				lfOptionList = MakeEmptyOptionList(listCode);
			_lfOptionList = lfOptionList;
			UpdateOptionListItemDictionaries(_lfOptionList);
		}

		public static string LcmOptionListName(string listCode)
		{
			if (MagicStrings.LcmOptionlistNames.ContainsKey(listCode))
				return MagicStrings.LcmOptionlistNames[listCode];

			return listCode;
		}

		public virtual LfOptionList PrepareOptionListUpdate(ICmPossibilityList lcmOptionList)
		{
			bool optionListDiffersFromOriginal = false;
			Dictionary<Guid, ICmPossibility> lcmOptionListByGuid = lcmOptionList.ReallyReallyAllPossibilities
				// .Where(poss => poss.Guid != null) // Not needed as ICmPossibility GUIDs are not nullable
				.ToDictionary(poss => poss.Guid);

			foreach (ICmPossibility poss in lcmOptionListByGuid.Values)
			{
				LfOptionListItem correspondingItem;
				if (_lfOptionListItemByGuid.TryGetValue(poss.Guid, out correspondingItem))
				{
					// One-way latch: once optionListDiffersFromOriginal becomes true, it should remain true.
					// Do NOT reorder this || expression. We want the function call *first* so that it will never be short-circuited away.
					optionListDiffersFromOriginal = SetOptionListItemFromCmPossibility(correspondingItem, poss) || optionListDiffersFromOriginal;
				}
				else
				{
					correspondingItem = CmPossibilityToOptionListItem(poss);
					optionListDiffersFromOriginal = true;
				}
				_lfOptionListItemByGuid[poss.Guid] = correspondingItem;
				_lfOptionListItemByStrKey[correspondingItem.Key] = correspondingItem;
			}

			var lfNewOptionList = CloneOptionListWithEmptyItems(_lfOptionList);
			// We filter by "Does LCM have an option list item with corresponding GUID?" because if it doesn't,
			// then that option list item was probably deleted in LCM, so we should delete it here.
			lfNewOptionList.Items = _lfOptionListItemByGuid.Values
				.Where(item => lcmOptionListByGuid.ContainsKey(item.Guid.GetValueOrDefault()))
				.ToList();

			if (lfNewOptionList.Items.Count != _lfOptionList.Items.Count)
			{
				// Deleted at least one item because it had disappeared from LCM
				optionListDiffersFromOriginal = true;
			}

			if (optionListDiffersFromOriginal)
			{
				lfNewOptionList.DateModified = DateTime.Now;  // TODO: Investigate why this was changed from UtcNow
			}

			return lfNewOptionList;
		}

		public string LfItemKeyString(ICmPossibility lcmOptionListItem, int ws)
		{
			string result;
			if (lcmOptionListItem == null)
				return null;

			if (_lfOptionList != null)
			{
				if (_lfOptionListItemKeyByGuid.TryGetValue(lcmOptionListItem.Guid, out result))
					return result;

				// We shouldn't get here, because the option list SHOULD be pre-populated.
				_logger.Error("Got an option list item without a corresponding LF option list item. " +
					"In option list name '{0}', list code '{1}': " +
					"LCM option list item '{2}' had GUID {3} but no LF option list item was found",
					_lfOptionList.Name, _lfOptionList.Code,
					lcmOptionListItem.AbbrAndName, lcmOptionListItem.Guid
				);
				return null;
			}

			if (lcmOptionListItem.Abbreviation == null || lcmOptionListItem.Abbreviation.get_String(ws) == null)
			{
				// Last-ditch effort
				char ORC = '\ufffc';
				return lcmOptionListItem.AbbrevHierarchyString.Split(ORC).LastOrDefault();
			}
			else
			{
				return ConvertLcmToMongoTsStrings.TextFromTsString(lcmOptionListItem.Abbreviation.get_String(ws), _wsf);
			}
		}

		// For multi-option lists; use like "LfStringArrayField.FromStrings(_converter.LfItemKeyStrings(PossibilityList), _wsEn)".
		public IEnumerable<string> LfItemKeyStrings(IEnumerable<ICmPossibility> lcmOptionListItems, int ws)
		{
			foreach (ICmPossibility lcmOptionListItem in lcmOptionListItems)
				yield return LfItemKeyString(lcmOptionListItem, ws);
		}

		protected LfOptionListItem CmPossibilityToOptionListItem(ICmPossibility pos)
		{
			var item = new LfOptionListItem();
			SetOptionListItemFromCmPossibility(item, pos, true);  // Ignore the bool result since this will always modify the item
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
			result.DateCreated = result.DateModified = DateTime.Now;  // TODO: Investigate why this was changed from UtcNow
			result.Code = listCode;
			result.Name = LcmOptionListName(listCode);
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
			newList.DateModified = original.DateModified;
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

		// Returns true if the item passed in has been modified at all, false otherwise
		protected bool SetOptionListItemFromCmPossibility(LfOptionListItem item, ICmPossibility poss, bool setKey = false)
		{
			bool modified = false;
			string abbreviation = ConvertLcmToMongoTsStrings.TextFromTsString(poss.Abbreviation.BestAnalysisVernacularAlternative, _wsf);
			if (item.Abbreviation != abbreviation)
			{
				modified = true;
			}
			item.Abbreviation = abbreviation;
			if (setKey)
			{
				string key = FindAppropriateKey(ConvertLcmToMongoTsStrings.TextFromTsString(poss.Abbreviation.get_String(_wsForKeys), _wsf));
				if (item.Key != key)
				{
					modified = true;
				}
				item.Key = key;
			}
			string value = ConvertLcmToMongoTsStrings.TextFromTsString(poss.Name.BestAnalysisVernacularAlternative, _wsf);
			if (item.Value != value)
			{
				modified = true;
			}
			item.Value = value;
			if (item.Guid != poss.Guid)
			{
				modified = true;
			}
			item.Guid = poss.Guid;
			return modified;
		}

	}
}
