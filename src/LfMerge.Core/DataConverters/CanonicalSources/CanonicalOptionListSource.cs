// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public abstract class CanonicalOptionListSource
	{
		// Currently, there are two option lists that should derive from a "canonical" source: grammatical info, and semantic domains

		protected Dictionary<Guid, CanonicalItem> byGuid;
		protected Dictionary<string, CanonicalItem> byKey;

		private string primaryElementName;
		private string resourceName;

		public CanonicalOptionListSource(string resourceName, string primaryElementName)
		{
			this.resourceName = resourceName;
			this.primaryElementName = primaryElementName;
			this.byGuid = new Dictionary<Guid, CanonicalItem>();
			this.byKey = new Dictionary<string, CanonicalItem>();
		}

		public static CanonicalOptionListSource Create(string listCode)
		{
			// TODO: Consider whether to make singletons, for the sake of not having to re-parse the resources.
			// However, I suspect that it won't be necessary, as CanonicalOptionListSource instances get attached
			// to ConvertMongoToLcmOptionList instances, and those instances stick around for one entire Lexicon conversion.
			// It appears to take 200ms to process semantic domains, and 30ms to process grammatical info. So it might
			// not be worth the extra complexity of singletons -- or it might. That remains to be decided.
			if (listCode == MagicStrings.LfOptionListCodeForGrammaticalInfo)
				return new CanonicalPartOfSpeechSource();
			else if (listCode == MagicStrings.LfOptionListCodeForSemanticDomains)
				return new CanonicalSemanticDomainSource();
			else if (listCode == MagicStrings.LfOptionListCodeForLfTags)
				return new CanonicalLfTagSource();
			else
				return null;
		}

		public CanonicalItem ByGuidOrNull(Guid g)
		{
			if (this.byGuid.Count == 0)
			{
				LoadCanonicalData();
			}
			CanonicalItem result;
			return byGuid.TryGetValue(g, out result) ? result : null;
		}

		public CanonicalItem ByKeyOrNull(string key){
			if (this.byKey.Count == 0)
			{
				LoadCanonicalData();
			}
			CanonicalItem result;
			return byKey.TryGetValue(key, out result) ? result : null;
		}

		public bool TryGetByGuid(Guid g, out CanonicalItem result)
		{
			result = ByGuidOrNull(g);
			return (result != null);
		}

		public bool TryGetByKey(string key, out CanonicalItem result)
		{
			result = ByKeyOrNull(key);
			return (result != null);
		}

		public Dictionary<Guid, CanonicalItem>.ValueCollection ValuesByGuid => this.byGuid.Values;
		public Dictionary<string, CanonicalItem>.ValueCollection ValuesByKey => this.byKey.Values;
		public int Count => this.byGuid.Count;

		// Descendants will override this to specify the generic type T.
		// E.g., LoadCanonicalData<CanonicalPartOfSpeechItem>();
		public abstract void LoadCanonicalData();

		public void LoadCanonicalData<T>()
			where T: CanonicalItem, new()
		{
			using (Stream stream = typeof(CanonicalOptionListSource).Assembly.GetManifestResourceStream(resourceName))
				LoadCanonicalData<T>(stream);
		}

		public void LoadCanonicalData<T>(Stream stream)
			where T: CanonicalItem, new()
		{
			using (var xmlReader = XmlReader.Create(stream))
				LoadCanonicalData<T>(xmlReader);
		}

		public void LoadCanonicalData<T>(XmlReader xmlReader)
			where T: CanonicalItem, new()
		{
			while (xmlReader.Read())
			{
				if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.LocalName == primaryElementName)
				{
					var item = new T();
					item.PopulateFromXml(xmlReader);
					UpdateDictsFromItem(item);
				}
			}
		}

		protected void UpdateDictsFromItem(CanonicalItem item)
		{
			Guid g = Guid.Empty;
			if (Guid.TryParse(item.GuidStr, out g))
			{
				// Update either both dicts, or neither.
				byGuid[g] = item;
				SetAppropriateKey(item);
				byKey[item.Key] = item;
			}
			foreach (CanonicalItem subitem in item.Subitems)
				UpdateDictsFromItem(subitem);
		}

		// Deal with duplicate keys by appending a simple number to them.
		// Should be called AFTER PopulateFromXml() has done its work.
		protected void SetAppropriateKey(CanonicalItem item)
		{
			string origKey = item.Key;
			int extra = 0;
			while (byKey.ContainsKey(item.Key))
			{
				extra++;
				item.Key = origKey + extra.ToString();
			}
		}
	}
}
