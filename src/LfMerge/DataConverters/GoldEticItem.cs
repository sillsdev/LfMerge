// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;

namespace LfMerge.DataConverters
{
	public class GoldEticItem
	{
		public string Id { get; private set; }
		public GoldEticItem Parent { get; private set; }
		public string Guid { get; private set; }
		public List<GoldEticItem> Subitems { get; private set; }
		public Dictionary<string, string> Abbrevs { get; private set; }
		public Dictionary<string, string> Terms { get; private set; }
		public Dictionary<string, string> Definitions { get; private set; }

		public string ORCDelimitedName {
			get
			{
				string ORC = "\ufffc";
				if (Parent != null)
					return Parent.ORCDelimitedName + ORC + Id;
				else
					return Id;
			}
		}

		public GoldEticItem(string id, string guid, GoldEticItem parent = null)
		{
			Id = id;
			Guid = guid;
			Parent = parent;
			Subitems = new List<GoldEticItem>();
			Abbrevs = new Dictionary<string, string>();
			Terms = new Dictionary<string, string>();
			Definitions = new Dictionary<string, string>();
			if (parent != null)
				parent.AddChild(this);
		}

		private void AddChild(GoldEticItem child)
		{
			Subitems.Add(child);
			// NOTE: We make no attempt to set child.Parent; that was done in the child's constructor.
			// We also never deal with the case where a child "moves" to a different parent, since we never do that.
		}

		public void AddAbbrev(string ws, string data)
		{
			Abbrevs.Add(ws, data);
		}

		public void AddTerm(string ws, string data)
		{
			Terms.Add(ws, data);
		}

		public void AddDefinition(string ws, string data)
		{
			Definitions.Add(ws, data);
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
			if (Terms.TryGetValue(SimplifyWs(ws), out name))
				return name;
			else
				return String.Empty;
		}

		public string ORCDelimitedNameByWs(string ws)
		{
			ws = SimplifyWs(ws);
			string ORC = "\ufffc";
			if (Parent != null)
				return Parent.ORCDelimitedNameByWs(ws) + ORC + NameByWs(ws);
			else
				return NameByWs(ws);
			// NOTE: Doesn't handle cases where some ancestors have names for that writing system but others don't.
		}

		public string AbbrevByWs(string ws)
		{
			string name;
			if (Abbrevs.TryGetValue(SimplifyWs(ws), out name))
				return name;
			else
				return String.Empty;
		}

		public string ORCDelimitedAbbrevByWs(string ws)
		{
			ws = SimplifyWs(ws);
			string ORC = "\ufffc";
			if (Parent != null)
				return Parent.ORCDelimitedAbbrevByWs(ws) + ORC + AbbrevByWs(ws);
			else
				return AbbrevByWs(ws);
			// NOTE: Doesn't handle cases where some ancestors have names for that writing system but others don't.
		}
	}
}

