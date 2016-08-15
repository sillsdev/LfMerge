// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public struct Run
	{
		public string Content;
		public string StyleName;
		public string Lang;
		public Guid? Guid;
	}

	public class ConvertMongoToFdoTsStrings
	{
		private static Regex spanRegex = new Regex("(<span[^>]*>.*?</span>)");
		private static Regex spanContentsRegex = new Regex(@"<span\s+(lang=""([^""]+)"")?\s*(class=""([^""]+)"")?\s*(lang=""([^""]+)"")?\s*>(.*?)</span\s*>");
		private static Regex styleRegex = new Regex("styleName_([^ ]+)");
		private static Regex guidRegex = new Regex("guid_([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");

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

		public static List<string> GetSpanTexts(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				result.Add(match.Groups[7].Value);
			}
			return result;
		}

		public static List<string> GetSpanLanguages(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				if (match.Groups[1].Success)
					result.Add(match.Groups[2].Value);
				else if (match.Groups[5].Success)
					result.Add(match.Groups[6].Value);
			}
			return result;
		}

		public static List<Guid> GetSpanGuids(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<Guid>();
			foreach (Match match in matches)
			{
				if (match.Groups[3].Success)
				{
					string[] classes = match.Groups[4].Value.Split(null);  // Split on any whitespace
					foreach (string cls in classes)
					{
						Guid g;
						Match m = guidRegex.Match(cls);
						if (m.Success && m.Groups[1].Success && Guid.TryParse(m.Groups[1].Value, out g))
							result.Add(g);
					}
				}
			}
			return result;
		}

		public static List<string> GetSpanStyles(string source)
		{
			MatchCollection matches = spanContentsRegex.Matches(source);
			var result = new List<string>();
			foreach (Match match in matches)
			{
				if (match.Groups[3].Success)
				{
					string[] classes = match.Groups[4].Value.Split(null);  // Split on any whitespace
					foreach (string cls in classes)
					{
						Match m = styleRegex.Match(cls);
						if (m.Success && m.Groups[1].Success)
							result.Add(m.Groups[1].Value);
					}
				}
			}
			return result;
		}

		public static List<Run> GetSpanRuns(string source)
		{
			string[] parts = spanRegex.Split(source);
			var result = new List<Run>();
			foreach (string part in parts)
			{
				Run run = new Run();
				run.Content = null;
				run.Lang = null;
				run.StyleName = null;
				run.Guid = null;
				Match match = spanContentsRegex.Match(part);
				if (!match.Success || match.Groups.Count < 8 || !match.Groups[7].Success)
				{
					// We're outside a span
					run.Content = part;
					result.Add(run);
					continue;
				}
				// We're inside a span
				run.Content = match.Groups[7].Value;
				if (match.Groups[1].Success && match.Groups[2].Success)
					run.Lang = match.Groups[2].Value;
				else if (match.Groups[5].Success && match.Groups[6].Success)
					run.Lang = match.Groups[6].Value;
				if (match.Groups[3].Success)
				{
					string[] classes = match.Groups[4].Value.Split(null);  // Split on any whitespace
					foreach (string cls in classes)
					{
						Match m = styleRegex.Match(cls);
						if (m.Success && m.Groups[1].Success)
							run.StyleName = m.Groups[1].Value;
						Guid g;
						m = guidRegex.Match(cls);
						if (m.Success && m.Groups[1].Success && Guid.TryParse(m.Groups[1].Value, out g))
							run.Guid = g;
					}
				}
				result.Add(run);
			}
			return result;
		}
	}
}

