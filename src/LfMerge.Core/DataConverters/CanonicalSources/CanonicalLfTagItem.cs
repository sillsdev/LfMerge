// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Xml;
using SIL.LCModel;

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalLfTagItem : CanonicalItem
	{
		public override void PopulateFromXml(XmlReader reader)
		{
			if (reader.LocalName != "item" || string.IsNullOrEmpty(reader.GetAttribute("guid")))
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
							if (!string.IsNullOrEmpty(reader.GetAttribute("id")))
							{
								Key = reader.GetAttribute("id");
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
						}
						break;
					}
				case XmlNodeType.EndElement:
					{
						if (reader.LocalName == "item")
						{
							if (string.IsNullOrEmpty(Key)) {
								Key = AbbrevByWs(KeyWs);
							}
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
			// CanonicalLfTagItem instances don't need anything from ExtraData
		}
	}
}
