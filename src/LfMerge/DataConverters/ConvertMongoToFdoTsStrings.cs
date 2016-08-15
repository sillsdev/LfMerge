// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoTsStrings
	{
		private static Regex spanRegex = new Regex("<span[^>]*>");
		private static Regex spanEndRegex = new Regex("</span[ ]*>");
		private static Regex spanContentsRegex = new Regex("<span[^>]*>([^<]*)</span[ ]*>");

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

		public static string[] GetSpanTexts(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				result.Add(match.Groups[1].Value);
			}
			return result.ToArray();
		}
	}
}

