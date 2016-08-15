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
		private static Regex spanContentsRegex = new Regex(@"<span\s+(lang=""([^""]+)"")?\s*(class=""([^""]+)"")?\s*(lang=""([^""]+)"")?\s*>(.*?)</span\s*>");

		public ConvertMongoToFdoTsStrings()
		{
		}

		public ITsString SpansToRuns(string source)
		{
			throw new NotImplementedException();
		}

		public static int SpanCount(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			return matches.Count;
		}

		public static string[] GetSpanTexts(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				result.Add(match.Groups[7].Value);
			}
			return result.ToArray();
		}

		public static string[] GetSpanLanguages(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				if (match.Groups[1].Success)
					result.Add(match.Groups[2].Value);
				else if (match.Groups[4].Success)
					result.Add(match.Groups[6].Value);
			}
			return result.ToArray();
		}
	}
}

