// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Text.RegularExpressions;

using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoTsStrings
	{
		private static Regex spanRegex = new Regex("<span[^>]*>");

		public ConvertMongoToFdoTsStrings()
		{
		}

		public ITsString SpansToRuns(string source)
		{
			throw new NotImplementedException();
		}

		public static int SpanCount(string source)
		{
			MatchCollection matches = spanRegex.Matches(source);
			return matches.Count;
		}
	}
}

