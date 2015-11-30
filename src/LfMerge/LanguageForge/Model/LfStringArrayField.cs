// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;

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
			return new LfStringArrayField { Values = new List<string>(source) };
		}

		public static LfStringArrayField FromSingleString(string source)
		{
			return new LfStringArrayField { Values = new List<string> { source } };
		}

		public static LfStringArrayField FromSingleITsString(ITsString source)
		{
			if (source == null || source.Text == null) return null;
			return LfStringArrayField.FromSingleString(source.Text);
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
			return LfStringArrayField.FromBestAnalysisVernaculars(
				possibilities.Select(multiString => multiString.Abbreviation));
		}

		public static LfStringArrayField FromSinglePossibilityAbbrev(ICmPossibility possibility)
		{
			if (possibility == null) return null;
			if (possibility.Abbreviation == null) return null;
			return LfStringArrayField.FromSingleITsString(possibility.Abbreviation.BestAnalysisVernacularAlternative);
		}
	}
}

