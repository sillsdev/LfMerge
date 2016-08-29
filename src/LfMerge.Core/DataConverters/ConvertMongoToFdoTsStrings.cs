// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.Core.DataConverters
{
	// TsString int properties have *two* ints, a value and a variation. The variation is only important for a few
	// property types, but we need to preserve it nonetheless.
	public struct IntProperty
	{
		public int Value;
		public int Variation;
		public IntProperty(int value, int variation)
		{
			Value = value;
			Variation = variation;
		}
	}

	public struct Run
	{
		public string Content;
		public string StyleName;
		public string Lang;
		public Dictionary<int, IntProperty> IntProperties;
		public Dictionary<int, string> StringProperties;
		public Guid? Guid;
	}

	public class ConvertMongoToFdoTsStrings
	{
		private static Regex spanRegex = new Regex("(<span[^>]*>.*?</span>)");
		private static Regex spanContentsRegex = new Regex(@"<span\s+(?<langAttr1>lang=""(?<langText1>[^""]+)"")?\s*(?<classAttr>class=""(?<classText>[^""]+)"")?\s*(?<langAttr2>lang=""(?<langText2>[^""]+)"")?\s*>(?<spanText>.*?)</span\s*>");
		private static Regex styleRegex = new Regex("styleName_([^ ]+)");
		private static Regex guidRegex = new Regex("guid_([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
		private static Regex intPropRegex = new Regex(@"propi_(?<propNum>\d+)_(?<propName>[^_]+)_(?<propValue>-?\d+)_(?<propVariation>\d+)");
		private static Regex strPropRegex = new Regex(@"props_(?<propNum>\d+)_(?<propName>[^_]+)_(?<propValue>.+)");

		public ConvertMongoToFdoTsStrings()
		{
		}

		public static string HtmlDecode(string encoded)
		{
			// System.Net.WebUtility.HtmlEncode and HtmlDecode is over-zealous (we do NOT want non-Roman characters
			// encoded, for example). So we have to write our own. Thankfully, it isn't hard at all.
			if (encoded == null)
				return null;
			return encoded.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
		}

		public static ITsString SpanStrToTsString(string source, int mainWs, ILgWritingSystemFactory wsf)
		{
			// How to build up an ITsString via an ITsIncStrBldr -
			// 1. Use SetIntPropValues or SetStrPropValues to set a property "to be applied to any subsequent append operations".
			// 2. THEN use Append(string s) to add a string, which will "pick up" the properties set in step 1.
			// See ScrFootnoteFactory.CreateRunFromStringRep() in FdoFactoryAdditions.cs for a good example.
			List<Run> runs = GetSpanRuns(source);
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			foreach (Run run in runs)
			{
				// To remove a string property, you set it to null, so we can just use StyleName directly whether or not it's null.
				builder.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, run.StyleName);
				int runWs = (run.Lang == null) ? mainWs : wsf.GetWsFromStr(run.Lang);
				builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, runWs);
				// We don't care about Guids in this function, so run.Guid is ignored
				// But we do need to set any other int or string properties that were in the original
				if (run.IntProperties != null)
					foreach (KeyValuePair<int, IntProperty> prop in run.IntProperties)
						builder.SetIntPropValues(prop.Key, prop.Value.Variation, prop.Value.Value);
				if (run.StringProperties != null)
					foreach (KeyValuePair<int, string> prop in run.StringProperties)
						builder.SetStrPropValue(prop.Key, prop.Value);
				builder.Append(run.Content);
			}
			return builder.GetString();
		}

		public static int SpanCount(string source)
		{
			List<Run> runs = GetSpanRuns(source);
			return runs.Where(RunWasSpan).Count();
		}

		public static bool RunWasSpan(Run run)
		{
			return run.Lang != null || run.StyleName != null || run.Guid != null;
		}

		public static IEnumerable<string> GetSpanTexts(string source)
		{
			IEnumerable<Run> runs = GetSpanRuns(source).Where(RunWasSpan);
			return runs.Select(run => run.Content);
		}

		public static IEnumerable<string> GetSpanLanguages(string source)
		{
			IEnumerable<Run> runs = GetSpanRuns(source).Where(run => run.Lang != null);
			return runs.Select(run => run.Lang);
		}

		public static IEnumerable<Guid> GetSpanGuids(string source)
		{
			IEnumerable<Run> runs = GetSpanRuns(source).Where(run => run.Guid != null);
			return runs.Select(run => run.Guid.Value);
		}

		public static IEnumerable<string> GetSpanStyles(string source)
		{
			IEnumerable<Run> runs = GetSpanRuns(source).Where(run => run.StyleName != null);
			return runs.Select(run => run.StyleName);
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
				if (!match.Success || match.Groups.Count < 8 || !match.Groups["spanText"].Success)
				{
					// We're outside a span
					run.Content = HtmlDecode(part);
					result.Add(run);
					continue;
				}
				// We're inside a span
				run.Content = HtmlDecode(match.Groups["spanText"].Value);
				if (match.Groups["langAttr1"].Success && match.Groups["langText1"].Success)
					run.Lang = match.Groups["langText1"].Value;
				else if (match.Groups["langAttr2"].Success && match.Groups["langText2"].Success)
					run.Lang = match.Groups["langText2"].Value;
				if (match.Groups["classAttr"].Success && match.Groups["classText"].Success)
				{
					string[] classes = match.Groups["classText"].Value.Split(null);  // Split on any whitespace
					foreach (string cls in classes)
					{
						Match m = styleRegex.Match(cls);
						if (m.Success && m.Groups[1].Success)
							run.StyleName = m.Groups[1].Value.Replace("_SPACE_", " ");
						Guid g;
						m = guidRegex.Match(cls);
						if (m.Success && m.Groups[1].Success && Guid.TryParse(m.Groups[1].Value, out g))
							run.Guid = g;
						m = intPropRegex.Match(cls);
						if (m.Success && m.Groups["propNum"].Success && m.Groups["propValue"].Success && m.Groups["propVariation"].Success)
						{
							if (run.IntProperties == null)
								run.IntProperties = new Dictionary<int, IntProperty>();
							int propNum;
							int propValue;
							int propVariation;
							if (Int32.TryParse(m.Groups["propNum"].Value, out propNum) &&
								Int32.TryParse(m.Groups["propValue"].Value, out propValue) &&
								Int32.TryParse(m.Groups["propVariation"].Value, out propVariation))
							{
								run.IntProperties[propNum] = new IntProperty(propValue, propVariation);
							}
						}
						m = strPropRegex.Match(cls);
						if (m.Success && m.Groups["propNum"].Success && m.Groups["propValue"].Success)
						{
							if (run.StringProperties == null)
								run.StringProperties = new Dictionary<int, string>();
							int propNum;
							if (Int32.TryParse(m.Groups["propNum"].Value, out propNum))
								run.StringProperties[propNum] = m.Groups["propValue"].Value.Replace("_SPACE_", " ");
						}
					}
				}
				result.Add(run);
			}
			return result;
		}
	}
}

