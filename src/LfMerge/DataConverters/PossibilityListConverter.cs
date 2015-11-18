// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
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
	}
}

