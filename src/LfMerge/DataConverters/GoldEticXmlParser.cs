// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace LfMerge
{
	public class GoldEticXmlParser
	{
		public GoldEticXmlParser()
		{
		}

		public List<GoldEticItem> ParseXml(Stream stream)
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

		public void AddAbbrev(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddAbbrev(ws, data);
		}

		public void AddTerm(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddTerm(ws, data);
		}

		public void AddDefinition(GoldEticItem item, string ws, string data)
		{
			if (item == null) return;
			item.AddDefinition(ws, data);
		}


	}
}

