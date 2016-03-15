// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringArrayField : LfFieldBase
	{
		public List<string> Values { get; set; }

		public bool IsEmpty { get { return Values.Count <= 0; } }

		public LfStringArrayField()
		{
			Values = new List<string>();
		}

		public static LfStringArrayField FromStrings(IEnumerable<string> source)
		{
			return new LfStringArrayField { Values = new List<string>(source.Where(s => s != null)) };
		}

		public static LfStringArrayField FromSingleString(string source)
		{
			if (source == null) return null;
			return new LfStringArrayField { Values = new List<string> { source } };
		}

		public static LfStringArrayField FromSingleITsString(ITsString source)
		{
			if (source == null || source.Text == null) return null;
			return FromSingleString(source.Text);
		}

		public static LfStringArrayField FromBestAnalysisVernaculars(IEnumerable<IMultiAccessorBase> source)
		{
			return new LfStringArrayField
			{
				Values = new List<string>(source.Select(multiString => multiString.BestAnalysisVernacularAlternative.Text))
			};
		}

		public static LfStringArrayField FromPossibilityAbbrevs(IEnumerable<ICmPossibility> possibilities)
		{
			return FromBestAnalysisVernaculars(
				possibilities.Select(multiString => multiString.Abbreviation));
		}

		public static LfStringArrayField FromPossibilityNames(IEnumerable<ICmPossibility> possibilities)
		{
			return FromBestAnalysisVernaculars(
				possibilities.Select(multiString => multiString.Name));
		}

		public static LfStringArrayField FromPossibilityAbbrevHierarchies(IEnumerable<ICmPossibility> possibilities)
		{
			return FromStrings(
				possibilities.Select(multiString => multiString.AbbrevHierarchyString));
		}

		public static LfStringArrayField FromPossibilityNameHierarchies(IEnumerable<ICmPossibility> possibilities)
		{
			return FromStrings(
				possibilities.Select(multiString => multiString.NameHierarchyString));
		}

		public static LfStringArrayField FromSinglePossibilityAbbrev(ICmPossibility possibility)
		{
			if (possibility == null) return null;
			if (possibility.Abbreviation == null) return null;
			return FromSingleITsString(possibility.Abbreviation.BestAnalysisVernacularAlternative);
		}
	}
}

