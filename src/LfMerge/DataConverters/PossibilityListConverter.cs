// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	public class PossibilityListConverter
	{
		public ICmPossibilityList Possibilities { get; private set; }

		public PossibilityListConverter(ICmPossibilityList possibilities)
		{
			Possibilities = possibilities;
		}

		// If necessary, could specify name or abbrev explicitly via a second parameter.
		public static string BestStringFrom(ICmPossibility possibility)
		{
			if (possibility == null)
				return null;
			IMultiUnicode stringSource = possibility.Name;
			if (stringSource == null)
				stringSource = possibility.Abbreviation;
			if (stringSource == null)
				return null;
			return stringSource.BestAnalysisVernacularAlternative.Text;
		}

		public ICmPossibility GetByName(string name)
		{
			foreach (ICmPossibility poss in Possibilities.ReallyReallyAllPossibilities)
			{
				if (BestStringFrom(poss) == name)
				{
					return poss;
				}
			}
			return null;
		}

		public ICmPossibility GetByName(LfStringField nameField)
		{
			if (nameField == null) return null;
			return GetByName(nameField.ToString());
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

