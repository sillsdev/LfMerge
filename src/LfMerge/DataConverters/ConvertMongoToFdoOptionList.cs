// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.DataConverters.CanonicalSources;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	// Currently, LF does not allow editing option lists on projects that send/receive to FDO.
	// If that ever changes, there's a bunch of commented-out (or #if false ... #endif'd) code below
	// that contains the start of an approach to handling that situation.
	public class ConvertMongoToFdoOptionList
	{
		protected IRepository<ICmPossibility> _possRepo;
		protected LfOptionList _lfOptionList;
		protected ILogger _logger;
		protected CanonicalOptionListSource _canonicalSource;
		#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
		protected int _wsForKeys;
		protected ICmPossibilityList _parentList;
		#endif

		public Dictionary<string, ICmPossibility> PossibilitiesByKey { get; protected set; }

		#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this version of the constructor
		public ConvertMongoToFdoOptionList(IRepository<ICmPossibility> possRepo, ILogger logger, ICmPossibilityList parentList, int wsForKeys, CanonicalOptionListSource canonicalSource = null)
		#endif
		public ConvertMongoToFdoOptionList(IRepository<ICmPossibility> possRepo, LfOptionList lfOptionList, ILogger logger, CanonicalOptionListSource canonicalSource = null)
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

			PossibilitiesByKey = new Dictionary<string, ICmPossibility>();
			if (lfOptionList == null || lfOptionList.Items == null)
				return;
			foreach (LfOptionListItem item in lfOptionList.Items)
			{
				ICmPossibility poss = LookupByItem(item);
				if (poss != null)
					PossibilitiesByKey[item.Key] = poss;
			}
		}

		public ICmPossibility FromStringKey(string key)
		{
			ICmPossibility result;
			if (PossibilitiesByKey.TryGetValue(key, out result))
				return result;
			#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
			if (_canonicalSource != null)
			{
				CanonicalItem item;
				if (_canonicalSource.TryGetByKey(key, out item))
				{
					return CreateFromCanonicalItem(item);
				}
			}
			#endif
			return null;
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
			ICmPossibility poss = _parentList.FindOrCreatePossibility(item.ORCDelimitedKey, wsForKeys);
			item.PopulatePossibility(poss);
			PossibilitiesByKey[item.Key] = poss;
			return poss;
		}
		#endif

		public ICmPossibility FromStringField(LfStringField keyField)
		{
			if (keyField == null)
				return null;
			string key = keyField.ToString();
			if (string.IsNullOrEmpty(key))
				return null;
			return FromStringKey(key);
		}

		public ICmPossibility FromStringArrayFieldWithOneCase(LfStringArrayField keyField)
		{
			if (keyField == null || keyField.Values == null || keyField.IsEmpty)
				return null;
			return FromStringKey(keyField.Values.First());
		}

		// Used in UpdatePossibilitiesFromStringArray and UpdateInvertedPossibilitiesFromStringArray below
		// Generic so that they can handle lists like AnthroCodes, etc.
		public IEnumerable<T> FromStringArrayField<T>(LfStringArrayField source)
		{
			IEnumerable<string> keys;
			if (source == null || source.Values == null)
				keys = new List<string>(); // Empty list
			else
				keys = source.Values.Where(value => !string.IsNullOrEmpty(value));
			return keys.Select(key => (T)FromStringKey(key)).Where(poss => poss != null);
		}

		protected ICmPossibility LookupByItem(LfOptionListItem item)
		{
			ICmPossibility result;
			if (item.Guid.HasValue)
			{
				if (_possRepo.TryGetObject(item.Guid.Value, out result))
					return result;
			}
			// return FromAbbrevAndName(item.Abbreviation, item.Value);
			return null;
		}

		// This function is generic because some lists, like ILexSense.AnthroCodesRC, are lists of interfaces *derived*
		// from ICmPossibility (e.g., ICmAnthroItem). This results in type errors at compile time: parameter
		// types like IFdoReferenceCollection<ICmPossibility> don't match IFdoReferenceCollection<ICmAnthroCode>.
		// Generics solve the problem, and can be automatically inferred by the compiler to boot.
		public void SetPossibilitiesCollection<T>(IFdoReferenceCollection<T> dest, IEnumerable<T> newItems)
			where T: class, ICmPossibility
		{
			// If we know of NO valid possibility keys, don't make any changes. That's because knowing of NO valid possibility keys
			// is FAR more likely to happen because of a bug than because we really removed an entire possibility list, and if there's
			// a bug, we shouldn't drop all the FDO data for this possibility list.
			if (PossibilitiesByKey.Count == 0)
				return;
			// We have to calculate the update (which items to remove and which to add) here; IFdoReferenceCollection won't do it for us.
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
		public void UpdatePossibilitiesFromStringArray<T>(IFdoReferenceCollection<T> dest, LfStringArrayField source)
			where T: class, ICmPossibility
		{
			SetPossibilitiesCollection(dest, FromStringArrayField<T>(source));
		}

		// Assumption: "source" contains valid keys. CAUTION: No error checking is done to ensure that this is true.
		// This is used for fields like DoNotPublishIn, where LF contains the "inverse" (a PublishIn field)
		public void UpdateInvertedPossibilitiesFromStringArray<T>(IFdoReferenceCollection<T> dest, LfStringArrayField source, IEnumerable<T> universeOfPossibilities)
			where T: class, ICmPossibility
		{
			// Start with everything
			HashSet<T> remainingPossibilities = new HashSet<T>(universeOfPossibilities);
			// Then deduct the possibilities found in the LF source field
			IEnumerable<T> possibilitesInCurrentField = FromStringArrayField<T>(source);
			foreach (T poss in possibilitesInCurrentField)
				remainingPossibilities.Remove(poss);
			// And set the collection to whatever's left over
			SetPossibilitiesCollection<T>(dest, remainingPossibilities.ToList());
		}

		#if false  // This was an older approach, but we MIGHT still revert back to it at some point, as a last-ditch fallback.
		public ICmPossibility FromAbbrevAndName(string abbrev, string name)
		{
		ICmPossibility poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, abbrev, _wsForKeys);
		if (poss == null)
		poss = _parentList.FindPossibilityByName(_parentList.PossibilitiesOS, name, _wsForKeys);
		return poss; // If it's still null, we just return null for "not found"
		// NOTE: If LF can create new OptionList items, might want to use FindOrCreatePossibilityByName above.
		}
		#endif
	}
}
