// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoPossibilityLists
	{
		public ICmPossibilityList Possibilities { get; private set; }
		public int WsForKeys { get; private set; }

		public ConvertMongoToFdoPossibilityLists(ICmPossibilityList possibilities, int wsForKeys)
		{
			Possibilities = possibilities;
			WsForKeys = wsForKeys;
		}

		// If necessary, could specify name or abbrev explicitly via a second parameter.
		public string BestStringFrom(ICmPossibility possibility)
		{
			if (possibility == null)
				return null;
			IMultiUnicode stringSource = possibility.Name;
			if (stringSource == null)
				stringSource = possibility.Abbreviation;
			if (stringSource == null)
				return null;
			return ConvertFdoToMongoTsStrings.SafeTsStringText(stringSource.get_String(WsForKeys));
			//return stringSource.BestAnalysisVernacularAlternative.Text;
		}

		public ICmPossibility GetByName(string name)
		{
			return Possibilities.ReallyReallyAllPossibilities.FirstOrDefault(poss => BestStringFrom(poss) == name);
		}

		public ICmPossibility GetByName(LfStringField nameField)
		{
			if (nameField == null) return null;
			return GetByName(nameField.ToString());
		}

		public ICmPossibility GetByKey(string key)
		{
			// return Possibilities.ReallyReallyAllPossibilities.FirstOrDefault(poss => BestStringFrom(poss) == key);
			return Possibilities.FindPossibilityByName(Possibilities.PossibilitiesOS, key, WsForKeys);
		}

		public ICmPossibility GetByKey(LfStringField keyField)
		{
			if (keyField == null || string.IsNullOrEmpty(keyField.ToString())) return null;
			return GetByKey(keyField.ToString());
		}

		/// <summary>
		/// Updates a possibility collection from an LfStringArray.
		/// The LfStringArray should contain keys from the ICmPossibilityList passed to this instance's constructor.
		/// CAUTION: No error checking is done to ensure that this is true.
		/// </summary>
		/// <param name="dest">Destination.</param>
		/// <param name="source">Source.</param>
		public void UpdatePossibilitiesFromStringArray(IFdoReferenceCollection<ICmPossibility> dest, LfStringArrayField source)
		{
			HashSet<string> sourceKeys = new HashSet<string>(source.Values);
			HashSet<ICmPossibility> itemsToRemove = new HashSet<ICmPossibility>();
			HashSet<ICmPossibility> itemsToAdd = new HashSet<ICmPossibility>();
			foreach (ICmPossibility poss in Possibilities.ReallyReallyAllPossibilities)
			{
				string possKey = BestStringFrom(poss);
				//string possKey = poss.NameHierarchyString; // TODO: This might be better than the other alternatives...
				if (sourceKeys.Contains(possKey) && !dest.Contains(poss))
					itemsToAdd.Add(poss);
			}
			foreach (ICmPossibility poss in dest)
			{
				if (!sourceKeys.Contains(poss.NameHierarchyString))
					itemsToRemove.Add(poss);
			}
			dest.Replace(itemsToRemove, itemsToAdd);
		}
	}
}

