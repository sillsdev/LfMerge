// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Xml;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public abstract class CanonicalItem
	{
		public const string ORC = "\ufffc";
		public const char ORCchar = '\ufffc';
		public const string KeyWs = "en";

		// Might come from an XML source like GOLDEtic.xml, or SemDom.xml
		// MUST have names and abbreviations
		// MAY have description, potentially other data (e.g., semdom has questions, example words, LouwNida or OCM codes, etc.)
		public string GuidStr { get; protected set; }  // Canonical GUID for this item. NOT a Guid struct type, but a string.
		public string Key { get; internal set; }   // Official abbreviation *IN ENGLISH*. Not to be confused with the abbreviation list.
		// Items are in a hiearchical list, and have parents and "children" (sub-items)
		public CanonicalItem Parent { get; protected set; }
		public List<CanonicalItem> Subitems { get; protected set; }
		// The following are keyed by writing system name (a string)
		public Dictionary<string, string> Abbrevs { get; protected set; }
		public Dictionary<string, string> Names { get; protected set; }
		public Dictionary<string, string> Descriptions { get; protected set; }
		// Extra data is keyed by field name, like "Questions", "Example Words", or whatever. Values are objects so that derived classes can do what they like with this.
		public Dictionary<string, object> ExtraData { get; protected set; }

		public string ORCDelimitedKey {
			get
			{
				if (Parent != null)
					return Parent.ORCDelimitedKey + ORC + Key;
				else
					return Key;
			}
		}

		public CanonicalItem()
		{
			GuidStr = System.Guid.Empty.ToString();
			Key = string.Empty;
			Parent = null;
			Subitems = new List<CanonicalItem>();
			Abbrevs = new Dictionary<string, string>();
			Names = new Dictionary<string, string>();
			Descriptions = new Dictionary<string, string>();
			ExtraData = new Dictionary<string, object>();
		}

		public override string ToString()
		{
			return $"{Key} ({GuidStr})";
		}

		/// <summary>
		/// Given an XmlReader positioned on this node's XML representation, populate its names, abbrevs, etc. from the XML.
		/// After running PopulateFromXml, the reader should be positioned just past this node's closing element.
		/// E.g., after parsing item 1 from <item id=1><name>Foo</name></item><!-- next node --> the reader should be positioned
		/// on the "next node" comment.
		/// </summary>
		/// <param name="reader">XmlReader instance, initially positioned on this node's XML representation.</param>
		public abstract void PopulateFromXml(XmlReader reader);

		/// <summary>
		/// Gets an item from a dictionary keyed by strings. If key is not present, creates and returns a new item.
		/// Since most derived classes will be storing some sort of lists in ExtraData (of questions, citations, examples, etc.),
		/// this will be a very useful helper function: e.g., it could return an empty list.
		/// </summary>
		/// <returns>The item corresponding to that key, or the newly-created item.</returns>
		/// <param name="dict">The dictionary to search.</param>
		/// <param name="key">Key of the item to return.</param>
		public T GetOrSetDefault<T>(IDictionary<string, object> dict, string key)
			where T: class, new()
		{
			if (dict.ContainsKey(key))
				return dict[key] as T;
			var result = new T();
			dict[key] = result;
			return result;
		}

		/// <summary>
		/// Like GetOrSetDefault, but specifically creating an empty list
		/// </summary>
		/// <returns>The list from dict.</returns>
		/// <param name="dict">Dict.</param>
		/// <param name="key">Key.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public List<T> GetListFromDict<T>(IDictionary<string, List<T>> dict, string key)
		{
			if (dict.ContainsKey(key))
				return dict[key];
			var result = new List<T>();
			dict[key] = result;
			return result;
		}

		/// <summary>
		/// Gets a list from the extra data dictionary by key. If key is not present, creates and returns an empty list.
		/// Since most derived classes will be storing some sort of lists in ExtraData (of questions, citations, examples, etc.),
		/// this will be a very useful helper function.
		/// </summary>
		/// <returns>The list corresponding to that key, or the newly-created empty list.</returns>
		/// <param name="key">Key.</param>
		/// <param name="defaultIfNotPresent">Default if not present.</param>
		public List<T> GetExtraDataList<T>(string key)
		{
			return GetOrSetDefault<List<T>>(ExtraData, key);
		}

		/// <summary>
		/// Similar to GetExtraDataList, but returns a dictionary keyed by writing system
		/// </summary>
		/// <returns>The dictionary corresponding to this key, or the newly-created empty dictionary.</returns>
		/// <param name="key">Key.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public Dictionary<string, List<T>> GetExtraDataWsDict<T>(string key)
		{
			return GetOrSetDefault<Dictionary<string, List<T>>>(ExtraData, key);
		}

		public void AppendChild(CanonicalItem child)
		{
			Subitems.Add(child);
			child.Parent = this;
		}

		public void AddAbbrev(string ws, string data)
		{
			Abbrevs.Add(ws, data);
		}

		public void AddName(string ws, string data)
		{
			Names.Add(ws, data);
		}

		public void AddDescription(string ws, string data)
		{
			Descriptions.Add(ws, data);
		}

		private string SimplifyWs(string ws)
		{
			if (ws.StartsWith("zh")) // The only non-2-letter writing system in GOLDEtic.xml is zh-CN
				return ws.Substring(0, 5);
			else
				return ws.Substring(0, 2);
		}

		public string NameByWs(string ws)
		{
			string name;
			if (Names.TryGetValue(SimplifyWs(ws), out name))
				return name;
			else
				return String.Empty;
		}

		public string ORCDelimitedNameByWs(string ws)
		{
			// NOTE: Doesn't handle cases where some ancestors have names for that writing system but others don't.
			ws = SimplifyWs(ws);
			string ORC = "\ufffc";
			if (Parent != null)
				return Parent.ORCDelimitedNameByWs(ws) + ORC + NameByWs(ws);
			else
				return NameByWs(ws);
		}

		public string AbbrevByWs(string ws)
		{
			string abbr;
			if (Abbrevs.TryGetValue(SimplifyWs(ws), out abbr))
				return abbr;
			else
				return String.Empty;
		}

		public string ORCDelimitedAbbrevByWs(string ws)
		{
			// NOTE: Doesn't handle cases where some ancestors have names for that writing system but others don't.
			ws = SimplifyWs(ws);
			string ORC = "\ufffc";
			if (Parent != null)
				return Parent.ORCDelimitedAbbrevByWs(ws) + ORC + AbbrevByWs(ws);
			else
				return AbbrevByWs(ws);
		}

		public void PopulatePossibility(ICmPossibility poss)
		// Creating, assigning parent, etc., is handled elsewhere. This function only sets text values.
		{
			ILgWritingSystemFactory wsf = poss.Cache.WritingSystemFactory;
			foreach (KeyValuePair<string, string> kv in Abbrevs)
			{
				int wsId = wsf.GetWsFromStr(kv.Key);
				if (wsId != 0)
					poss.Abbreviation.set_String(wsId, kv.Value);
			}
			foreach (KeyValuePair<string, string> kv in Descriptions)
			{
				int wsId = wsf.GetWsFromStr(kv.Key);
				if (wsId != 0)
					poss.Description.set_String(wsId, kv.Value);
			}
			foreach (KeyValuePair<string, string> kv in Names)
			{
				int wsId = wsf.GetWsFromStr(kv.Key);
				if (wsId != 0)
					poss.Name.set_String(wsId, kv.Value);
			}
			PopulatePossibilityFromExtraData(poss);
		}

		protected abstract void PopulatePossibilityFromExtraData(ICmPossibility poss);
	}
}
