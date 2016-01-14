// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace LfMerge.DataConverters
{
	public static class GoldEticXmlParser
	{
		public static List<GoldEticItem> ParseXml(Stream stream)
		{
			var settings = new XmlReaderSettings
			{
				IgnoreComments = true
			};
			List<GoldEticItem> result = new List<GoldEticItem>();
			using (var reader = XmlReader.Create(stream, settings))
			{
				GoldEticItem current = null;
				while (reader.Read())
				{
					switch (reader.NodeType)
					{
					case XmlNodeType.Element:
						{
							switch (reader.LocalName)
							{
							case "item":
								if (reader.GetAttribute("type") == "category")
									current = new GoldEticItem(reader.GetAttribute("id"), reader.GetAttribute("guid"), current);
								break;
							case "abbrev":
								AddAbbrev(current, reader.GetAttribute("ws"), reader.ReadInnerXml());
								break;
							case "term":
								AddTerm(current, reader.GetAttribute("ws"), reader.ReadInnerXml());
								break;
							case "def":
								AddDefinition(current, reader.GetAttribute("ws"), reader.ReadInnerXml());
								break;
							case "citation":
								// Ignore citations
								break;
							}
							break;
						}
					case XmlNodeType.EndElement:
						if (reader.LocalName == "item")
						{
							if (current.Parent == null)
								result.Add(current);
							current = current.Parent;
						}
						break;
					}
				}
			}
			return result;
		}

		public static void AddAbbrev(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddAbbrev(ws, data);
		}

		public static void AddTerm(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddTerm(ws, data.ToLowerInvariant()); // LanguageForge will be supplying lower-case terms to match against
		}

		public static void AddDefinition(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddDefinition(ws, data);
		}

	}
}

