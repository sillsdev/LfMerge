// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge
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

		public void AddChild(GoldEticItem child)
		{
			this.Subitems.Add(child);
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

		public string NameByWs(string ws)
		{
			string name;
			if (Terms.TryGetValue(ws, out name))
				return name;
			else
				return String.Empty;
		}

		public string ORCDelimitedNameByWs(string ws)
		{
			string ORC = "\ufffc";
			if (Parent != null)
				return Parent.ORCDelimitedNameByWs(ws) + ORC + NameByWs(ws);
			else
				return NameByWs(ws);
			// NOTE: Doesn't handle cases where some ancestors have names for that writing system but others don't.
		}
	}
}

