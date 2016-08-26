// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalPartOfSpeechItem : CanonicalItem
	{
		public override void PopulateFromXml(XmlReader reader)
		{
			if (reader.LocalName != "item" || reader.GetAttribute("type") != "category")
				return;  // If we weren't on the right kind of node, do nothing
			GuidStr = reader.GetAttribute("guid");
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
							{
								var child = new CanonicalPartOfSpeechItem();
								child.PopulateFromXml(reader);
								AppendChild(child);
							}
							break;
						case "abbrev":
							AddAbbrev(reader.GetAttribute("ws"), reader.ReadInnerXml());
							break;
						case "term":
							AddName(reader.GetAttribute("ws"), reader.ReadInnerXml());
							break;
						case "def":
							AddDescription(reader.GetAttribute("ws"), reader.ReadInnerXml());
							break;
						case "citation":
							Dictionary<string, List<string>> citationsDict = GetExtraDataWsDict<string>("citations");
							string ws = reader.GetAttribute("ws");
							List<string> citations = GetListFromDict<string>(citationsDict, ws);
							citations.Add(reader.ReadInnerXml());
							// No need to set anything in ExtraData, as GetListFromDict did it for us.
							break;
						}
						break;
					}
				case XmlNodeType.EndElement:
					{
						if (reader.LocalName == "item")
						{
							Key = AbbrevByWs(KeyWs);
							reader.Read(); // Skip past the closing element before returning
							return;
						}
						break;
					}
				}
			}
		}

		protected override void PopulatePossibilityFromExtraData(ICmPossibility poss)
		{
			// IPartOfSpeech instances don't need anything from ExtraData
		}
	}
}
