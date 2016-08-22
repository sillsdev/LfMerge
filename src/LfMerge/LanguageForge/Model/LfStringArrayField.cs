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
	}
}

