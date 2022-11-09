// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.DataConverters.CanonicalSources;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using SIL.LCModel;

namespace LfMerge.Core.DataConverters
{
	// Currently, LF does not allow editing option lists on projects that send/receive to Lcm.
	// If that ever changes, there's a bunch of commented-out (or #if false ... #endif'd) code below
	// that contains the start of an approach to handling that situation.
	public class ConvertMongoToLcmOptionList
	{
		protected IRepository<ICmPossibility> _possRepo;
		protected LfOptionList _lfOptionList;
		protected ILogger _logger;
		protected CanonicalOptionListSource _canonicalSource;
		#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
		protected int _wsForKeys;
		protected ICmPossibilityList _parentList;
		#endif

		public Dictionary<Guid, ICmPossibility> Possibilities { get; protected set; }

		#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this version of the constructor
		public ConvertMongoToLcmOptionList(IRepository<ICmPossibility> possRepo, LfOptionList lfOptionList, ILogger logger, ICmPossibilityList parentList, int wsForKeys, CanonicalOptionListSource canonicalSource = null)
		#endif
		public ConvertMongoToLcmOptionList(IRepository<ICmPossibility> possRepo, LfOptionList lfOptionList, ILogger logger, CanonicalOptionListSource canonicalSource = null)
		{
			_possRepo = possRepo;
			_logger = logger;
			#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
			_parentList = parentList;
			_wsForKeys = wsForKeys;
			#endif
			_canonicalSource = canonicalSource;
			RebuildLookupTables(lfOptionList);
		}

		// This method is public just in case, although I don't anticipate its being needed in user code
		public virtual void RebuildLookupTables(LfOptionList lfOptionList)
		{
			_lfOptionList = lfOptionList;

			Possibilities = new Dictionary<Guid, ICmPossibility>();
			if (lfOptionList == null || lfOptionList.Items == null)
				return;
			foreach (LfOptionListItem item in lfOptionList.Items)
			{
				ICmPossibility poss = LookupByItem(item);
				if (poss != null)
					Possibilities[item.Guid.Value] = poss;
			}
		}

		#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
		public ICmPossibility CreateFromCanonicalItem(CanonicalItem item)
		{
			if (item.Parent != null)
			{
				// Note that we're throwing away the return value; we want this for its side effects (it will create
				// and populate the parent if the parent didn't exist already).
				FromStringKey(item.Parent.Key);
			}
			ICmPossibility poss = _parentList.FindOrCreatePossibility(item.ORCDelimitedKey, _wsForKeys);
			item.PopulatePossibility(poss);
			PossibilitiesByKey[item.Key] = poss;
			return poss;
		}
		#endif

		public ICmPossibility LookupByItem(LfOptionListItem item)
		{
			if (item == null)
				return null;
			ICmPossibility result;
			if (item.Guid.HasValue)
			{
				if (Possibilities.TryGetValue(item.Guid.Value, out result))
					return result;
				else if (_possRepo.TryGetObject(item.Guid.Value, out result))
					return result;
				else if (_canonicalSource != null)
					return LookupByCanonicalItem(_canonicalSource.ByKeyOrNull(item.Value));
			}
			#if false  // Once we are populating Lcm from LF, we might also need to fall back to abbreviation and name for these lookups, because Guids might not be available
			return FromAbbrevAndName(item.Abbreviation, item.Value);
			#endif
			return null;
		}

		protected ICmPossibility LookupByCanonicalItem(CanonicalItem item)
		{
			if (item == null)
				return null;
			ICmPossibility result;
			if (!String.IsNullOrEmpty(item.GuidStr))
			{
				Guid guid;
				if (Guid.TryParse(item.GuidStr, out guid))
				{
					if (_possRepo.TryGetObject(guid, out result))
						return result;
				}
			}
			#if false  // Once we are populating Lcm from LF, we might also need to fall back to abbreviation and name for these lookups, because Guids might not be available
			return FromAbbrevAndName(item.Abbrevs[_wsForKeys], item.Names[_wsForKeys]);
			#endif
			return null;
		}

		// This function is generic because some lists, like ILexSense.AnthroCodesRC, are lists of interfaces *derived*
		// from ICmPossibility (e.g., ICmAnthroItem). This results in type errors at compile time: parameter
		// types like ILcmReferenceCollection<ICmPossibility> don't match ILcmReferenceCollection<ICmAnthroCode>.
		// Generics solve the problem, and can be automatically inferred by the compiler to boot.
		private void SetPossibilitiesCollection<T>(ILcmReferenceCollection<T> dest, IEnumerable<T> newItems)
			where T: class, ICmPossibility
		{
			// If we know of NO valid possibility keys, don't make any changes. That's because knowing of NO valid possibility keys
			// is FAR more likely to happen because of a bug than because we really removed an entire possibility list, and if there's
			// a bug, we shouldn't drop all the Lcm data for this possibility list.
			if (Possibilities.Count == 0 && _canonicalSource == null)
				return;
			// We have to calculate the update (which items to remove and which to add) here; ILcmReferenceCollection won't do it for us.
			List<T> itemsToAdd = newItems.ToList();
			HashSet<Guid> guidsToAdd = new HashSet<Guid>(itemsToAdd.Select(poss => poss.Guid));
			List<T> itemsToRemove = new List<T>();
			foreach (T poss in dest)
			{
				if (!guidsToAdd.Contains(poss.Guid))
					itemsToRemove.Add(poss);
			}
			dest.Replace(itemsToRemove, itemsToAdd);
		}

		// Assumption: "source" contains valid keys. CAUTION: No error checking is done to ensure that this is true.
		public void UpdatePossibilitiesFromStringArray<T>(ILcmReferenceCollection<T> dest, List<LfOptionListItem> source)
			where T: class, ICmPossibility
		{
			var list = from s in source select (T)LookupByItem(s);
	  	SetPossibilitiesCollection(dest, list);
		}

		// Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
		#if false  // Once we are populating Lcm from LF OptionLists, the entire block of functions below will be useful. Until then, they're commented out.
		public ICmPossibility FromAbbrevAndName(string abbrev, string name, string userWs)
		{
		ICmPossibility poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, abbrev, _wsForKeys);
		if (poss == null)
		poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, name, _wsForKeys);
		return poss; // If it's still null, we just return null for "not found"
		// NOTE: If LF can create new OptionList items, might want to use FindOrCreatePossibilityByName above.
		}

		// Should be called from within an UndoableUnitOfWorkHelper
		// Replacement for the old UpdateLcmGrammarFromLfGrammar() method in ConvertMongoToLcmLexicon
		// Writing system should be the user interface language from LF. If not supplied, will default to English.
		public void UpdateLcmOptionListFromLf(string lfUserInterfaceWs = null)
		{
			foreach (LfOptionListItem item in _lfOptionList.Items)
			{
				UpdateOrCreatePossibilityFromOptionListItem(item, lfUserInterfaceWs);
			}
		}

		/// <summary>
		/// Update a CmPossibility object from the corresponding LF item. Will draw from canonical sources if available.
		/// Will try very hard to find a CmPossibility item matching the LF OptionList item, but will eventually create
		/// one if nothing remotely matching could be found.
		///
		/// NOTE: This is currently commented out (via #if false...#endif block) because we currently don't want to update
		/// Lcm from the values of LF option lists. Once that changes, this code can be uncommented.
		/// </summary>
		/// <param name="item">Item.</param>
		/// <param name="wsForOptionListItems">Ws for option list items.</param>
		public void UpdateOrCreatePossibilityFromOptionListItem(LfOptionListItem item, string wsForOptionListItems = null)
		{
			ICmPossibility poss = null;
			CanonicalItem canonicalItem = null;
			if (item.Guid != null)
			{
				if (_possRepo.TryGetObject(item.Guid.Value, out poss))
				{
					// Currently we do NOT want to change the name, abbreviation, etc. in Lcm for already-existing possibility items.
					// Once we do, uncomment the next line:
					//PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
					// For now, however, just return without touching the Lcm CmPossibility object
					return;
				}
				else
				{
					if (_canonicalSource != null && _canonicalSource.TryGetByGuid(item.Guid.Value, out canonicalItem))
					{
						canonicalItem.PopulatePossibility(poss);
						return;
					}
					// No canonical item? At least set name and abbreviation from LF
					var factory = _parentList.Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>();
					poss = factory.Create(item.Guid.Value, _parentList);  // Note that this does NOT handle "parent" possibilities; new one gets created as a TOP-level item
					PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
				}
			}
			else
			{
				// Can't look it up by GUID, so search by key. If key not found, fall back to abbreviation and name.
				if (_canonicalSource != null && _canonicalSource.TryGetByKey(item.Key, out canonicalItem))
				{
					canonicalItem.PopulatePossibility(poss);
					return;
				}
				// No canonical source? Then we're in fallback-of-fallback land. First try the key, as that's most likely to be found.
				poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, item.Key, _wsForKeys);
				if (poss != null)
				{
					PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
					return;
				}
				// Then try the abbreviation, and finally the name -- these, though, should be in the LF user's interface language
				poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, item.Abbreviation, wsForOptionListItems);
				if (poss != null)
				{
					PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
					return;
				}
				// In LF, OptionListItems have their name in the "Value" property
				poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, item.Value, wsForOptionListItems);
				if (poss != null)
				{
					PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
					return;
				}
				// If we STILL haven't found it, then just create a new item and populate it. This is the final, last-ditch fallback.
				poss = _parentList.FindOrCreatePossibility(item.Value, wsForOptionListItems);
				PopulateCmPossibilityFromOptionListItem(poss, item, wsForOptionListItems);
			}
		}

		public void PopulateCmPossibilityFromOptionListItem(ICmPossibility poss, LfOptionListItem item, string wsStr = null)
		{
			// Should only be called when NO canonical item can be found
			if (wsStr == null)
				wsStr = "en";  // TODO: Set this from LF user's writing system rather than English, once LF allows setting interface languages
			int wsId = poss.Cache.WritingSystemFactory.GetWsFromStr(wsStr);
			poss.Abbreviation.set_String(wsId, item.Abbreviation);
			poss.Name.set_String(wsId, item.Value);
		}
		#endif
	}
}
