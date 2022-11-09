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

			_lfOptionListItemByGuid = lfOptionList.Items
				.Where(item => item.Guid != null)
				.ToDictionary(item => item.Guid.Value, item => item);
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

		protected LfOptionListItem CmPossibilityToOptionListItem(ICmPossibility pos)
		{
			var item = new LfOptionListItem();
			SetOptionListItemFromCmPossibility(item, pos);  // Ignore the bool result since this will always modify the item
			return item;
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

		// Returns true if the item passed in has been modified at all, false otherwise
		protected bool SetOptionListItemFromCmPossibility(LfOptionListItem item, ICmPossibility poss)
		{
			bool modified = false;
			string abbreviation = ConvertLcmToMongoTsStrings.TextFromTsString(poss.Abbreviation.BestAnalysisVernacularAlternative, _wsf);
			if (item.Abbreviation != abbreviation)
			{
				modified = true;
			}
			item.Abbreviation = abbreviation;
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
