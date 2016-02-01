// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	public class PartOfSpeechConverter
	{
		private FdoCache _cache;
		private IPartOfSpeechRepository _posRepo;
		private IPartOfSpeechFactory _posFactory;

		public PartOfSpeechConverter(FdoCache fdoCache)
		{
			_cache = fdoCache;
			_posRepo = _cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>();
			_posFactory = _cache.ServiceLocator.GetInstance<IPartOfSpeechFactory>();
		}

		public static Lazy<Stream> GoldEticXml = new Lazy<Stream>(() =>
			typeof(MainClass).Assembly.GetManifestResourceStream(typeof(MainClass), "GOLDEtic.xml")
		);
		public static Lazy<List<GoldEticItem>> GoldEticItems = new Lazy<List<GoldEticItem>>(() =>
			GoldEticXmlParser.ParseXml(GoldEticXml.Value)
		);

		private static IEnumerable<GoldEticItem> FlattenGoldEticItems(IEnumerable<GoldEticItem> topLevel)
		{
			foreach (GoldEticItem item in topLevel)
			{
				yield return item;
				foreach (GoldEticItem subItem in FlattenGoldEticItems(item.Subitems))
					yield return subItem;
			}
		}
		public static Lazy<GoldEticItem[]> FlattenedGoldEticItems = new Lazy<GoldEticItem[]>(() =>
			FlattenGoldEticItems(GoldEticItems.Value).ToArray()
		);
		public static Lazy<Dictionary<string, GoldEticItem>> ItemsByGuidStr = new Lazy<Dictionary<string, GoldEticItem>>(() =>
			FlattenedGoldEticItems.Value.ToDictionary(item => item.Guid, item => item)
		);

		public string NameFromGuid(Guid guid, bool flat=false)
		{
			return NameFromGuidStr(guid.ToString(), flat);
		}

		public string NameFromGuidStr(string guidStr, bool flat=false)
		{
			string result;
			Dictionary<string, string> lookupTable = flat ?
				PartOfSpeechMasterList.FlatPosNames :
				PartOfSpeechMasterList.HierarchicalPosNames;
			if (lookupTable.TryGetValue(guidStr, out result))
				return result;
			return null;
		}

		public IPartOfSpeech FromGuid(Guid guid)
		{
			return FromGuidStr(guid.ToString());
		}

		public IPartOfSpeech FromGuidStr(string guidStr)
		{
			IPartOfSpeech result;
			if (TryGetPos(guidStr, out result))
				return result;
			string name = NameFromGuidStr(guidStr, flat:false);
			if (name == null) return null;
			// We know we have a well-known name, so we should use it. TODO: Or should we?
			Guid guid;
			if (Guid.TryParse(guidStr, out guid))
				return CreateFromWellKnownGuid(guid);
			// Really shouldn't get here at all, but if we do...
			throw new ArgumentException(String.Format("Well-known GUID {0} didn't parse", guidStr), "guidStr");
			// TODO: Turn that into a log message and don't actually throw an exception.
		}

		public IPartOfSpeech FromGuidStrOrName(string guidStr, string name, string userWs = "en", string fallbackWs = "en")
		{
			IPartOfSpeech posFromGuid = FromGuidStr(guidStr);
			if (posFromGuid != null) return posFromGuid;
			return FromName(name, userWs, fallbackWs);
		}

		public static IPartOfSpeech FromMSA(IMoMorphSynAnalysis msa)
		{
			switch (msa.ClassID)
			{
			case MoDerivAffMsaTags.kClassId:
				// TODO: Turn this into a log message, and try to make the log message a little clearer to non-linguists, if possible.
				Console.WriteLine("For derivational affix {0}, arbitrarily picking \"To\" part of speech instead of the \"From\" part of speech.", msa.GetGlossOfFirstSense());
				return ((IMoDerivAffMsa)msa).ToPartOfSpeechRA;
			case MoDerivStepMsaTags.kClassId:
				return ((IMoDerivStepMsa)msa).PartOfSpeechRA;
			case MoInflAffMsaTags.kClassId:
				return ((IMoInflAffMsa)msa).PartOfSpeechRA;
			case MoStemMsaTags.kClassId:
				return ((IMoStemMsa)msa).PartOfSpeechRA;
			case MoUnclassifiedAffixMsaTags.kClassId:
				return ((IMoUnclassifiedAffixMsa)msa).PartOfSpeechRA;
			default:
				// TODO: Make this a log message, not Console.WriteLine
				Console.WriteLine("Got MSA of unknown type {0}", msa.GetType().Name);
				return null;
			}
		}

		public static void SetPartOfSpeech(IMoMorphSynAnalysis msa, IPartOfSpeech pos, IPartOfSpeech secondaryPos = null)
		{
			if (msa == null)
			{
				Console.WriteLine("msa is null!"); // TODO: Turn this into proper log message
				// throw new ArgumentNullException("msa");
				return; // TODO: Or throw an ArgumentNullException?
			}
			if (pos == null)
			{
				Console.WriteLine("pos is null!"); // TODO: Turn this into proper log message
				// throw new ArgumentNullException("pos");
				return; // TODO: Or throw an ArgumentNullException?
			}
			Console.WriteLine("Setting part of speech {0} ({1}) in msa {2}", pos.NameHierarchyString, pos.Guid, msa.Guid);
			// TODO: Is the below switch statement REALLY complete? Or do we need to do more?
			// See FdoFactoryAdditions.cs, lines 1603-1698: perhaps we should be using factories and SandboxMSA objects?
			switch (msa.ClassID)
			{
			case MoDerivAffMsaTags.kClassId:
				((IMoDerivAffMsa)msa).FromPartOfSpeechRA = pos;
				if (secondaryPos != null)
					((IMoDerivAffMsa)msa).ToPartOfSpeechRA = secondaryPos;
				break;
			case MoDerivStepMsaTags.kClassId:
				((IMoDerivStepMsa)msa).PartOfSpeechRA = pos;
				break;
			case MoInflAffMsaTags.kClassId:
				((IMoInflAffMsa)msa).PartOfSpeechRA = pos;
				break;
			case MoStemMsaTags.kClassId:
				((IMoStemMsa)msa).PartOfSpeechRA = pos;
				break;
			case MoUnclassifiedAffixMsaTags.kClassId:
				((IMoUnclassifiedAffixMsa)msa).PartOfSpeechRA = pos;
				break;
			default:
				// TODO: Make this a log message, not Console.WriteLine
				Console.WriteLine("Got MSA of unknown type {0}", msa.GetType().Name);
				return;
			}
		}

		private GoldEticItem FindGoldEticItem(string searchTerm, string wsToSearch)
		{
			foreach (GoldEticItem item in FlattenedGoldEticItems.Value)
				if (item.ORCDelimitedNameByWs(wsToSearch) == searchTerm || item.NameByWs(wsToSearch) == searchTerm)
					return item;
			return null;
		}

		private void PopulateWellKnownPos(IPartOfSpeech pos, GoldEticItem item)
		// Creating, assigning parent, etc., is handled elsewhere. This function only sets text values.
		{
			foreach (var kv in item.Abbrevs)
			{
				int wsId = _cache.WritingSystemFactory.GetWsFromStr(kv.Key);
				if (wsId != 0)
					pos.Abbreviation.set_String(wsId, kv.Value);
			}
			foreach (var kv in item.Definitions)
			{
				int wsId = _cache.WritingSystemFactory.GetWsFromStr(kv.Key);
				if (wsId != 0)
					pos.Description.set_String(wsId, kv.Value);
			}
			foreach (var kv in item.Terms)
			{
				int wsId = _cache.WritingSystemFactory.GetWsFromStr(kv.Key);
				if (wsId != 0)
					pos.Name.set_String(wsId, kv.Value);
			}
		}

		private void PopulateCustomPos(IPartOfSpeech pos, string finalName, string userWs)
		{
			int wsId = _cache.WritingSystemFactory.GetWsFromStr(userWs);
			pos.Name.set_String(wsId, finalName);
		}

		private bool TryGetPos(string guidStr, out IPartOfSpeech result)
		{
			Guid guid = Guid.Empty;
			if (!Guid.TryParse(guidStr, out guid) || guid == Guid.Empty)
			{
				result = null;
				return false;
			}
			return _posRepo.TryGetObject(guid, out result);
		}

		private IPartOfSpeech GetOrCreateTopLevelPos(string guidStr)
		{
			IPartOfSpeech existingPos;
			if (TryGetPos(guidStr, out existingPos))
				return existingPos;
			Guid guid;
			Guid.TryParse(guidStr, out guid); // Don't care if this fails
			return _posFactory.Create(guid, _cache.LanguageProject.PartsOfSpeechOA);
		}

		private IPartOfSpeech GetOrCreateOwnedPos(string guidStr, IPartOfSpeech owner)
		{
			IPartOfSpeech existingPos;
			if (TryGetPos(guidStr, out existingPos))
				return existingPos;
			Guid guid;
			Guid.TryParse(guidStr, out guid); // Don't care if this fails
			return _posFactory.Create(guid, owner);
		}

		public IPartOfSpeech CreateFromWellKnownItem(GoldEticItem item)
		{
			IPartOfSpeech pos;
			if (item.Parent == null)
			{
				string guidStr = item.Guid;
				Guid guid;
				if (!Guid.TryParse(guidStr, out guid))
				{
					// TODO: Log this
					Console.WriteLine("ERROR: GOLDEtic.xml item {0} had invalid GUID {1}", item.ORCDelimitedName, item.Guid);
					return null;
				}
				pos = GetOrCreateTopLevelPos(item.Guid);
			}
			else
			{
				IPartOfSpeech fdoParent = CreateFromWellKnownItem(item.Parent);
				pos = GetOrCreateOwnedPos(item.Guid, fdoParent);
			}
			PopulateWellKnownPos(pos, item);
			return pos;
		}

		public IPartOfSpeech CreateFromWellKnownGuid(Guid guid)
		{
			GoldEticItem item;
			if (!ItemsByGuidStr.Value.TryGetValue(guid.ToString(), out item))
				return null;
			return CreateFromWellKnownItem(item);
		}

		public IPartOfSpeech CreateFromCustomName(string nameHierarchy, string userWs = "en")
		{
			// TODO: Verify that this handles "A|B|c" and "A|b|c" cases, where part of the name is official
			// (Because I think if A does not yet exist, and "A|b|c" is found, A might get the wrong GUID.)
			int wsId = _cache.WritingSystemFactory.GetWsFromStr(userWs);
			return (IPartOfSpeech)_cache.LangProject.PartsOfSpeechOA.FindOrCreatePossibility(nameHierarchy, wsId, true);
		}

		// TODO: Rename this function, then remove this comment
		// TODO: This might be a duplicate by now, in which case it should be removed
		private void RenameThisFunction(IPartOfSpeech pos, GoldEticItem item, string userSuppliedName = null, string userWs = "en")
		{
			if (item != null)
			{
				PopulateWellKnownPos(pos, item);
			}
			else
			{
				if (userSuppliedName == null)
					throw new ArgumentNullException("For non-well-known parts of speech, we need at least a name!");
				PopulateCustomPos(pos, userSuppliedName, userWs);
			}
			if (item != null && item.Parent != null)
			{
				IPartOfSpeech parent;
				Guid guid = Guid.Empty;
				Guid.TryParse(item.Guid, out guid);
				if (guid != Guid.Empty && _posRepo.TryGetObject(guid, out parent))
					Console.WriteLine(parent);
				else
					Console.WriteLine("Create parent as per this function...");
			}
		}

		public IPartOfSpeech FromName(string name, string wsToSearch = "en", string fallbackWs = "en")
		{
			string guidStr;
			string foundWs = fallbackWs;
			GoldEticItem item = null;
			Guid guid = Guid.Empty;

			// Try four different ways to look up this name in GOLDEtic
			if (!PartOfSpeechMasterList.HierarchicalPosGuids.TryGetValue(name, out guidStr))
				guidStr = null;
			if (guidStr == null && !PartOfSpeechMasterList.FlatPosGuids.TryGetValue(name, out guidStr))
				guidStr = null;
			if (guidStr == null)
			{
				item = FindGoldEticItem(name, wsToSearch);
				if (item != null)
				{
					guidStr = item.Guid;
					foundWs = wsToSearch;
				}
			}
			if (guidStr == null)
			{
				item = FindGoldEticItem(name, fallbackWs);
				if (item != null)
				{
					guidStr = item.Guid;
					foundWs = fallbackWs;
				}
			}
			if (guidStr != null)
			{
				if (!Guid.TryParse(guidStr, out guid))
					guid = Guid.Empty;
			}

			// So... did we find it?
			if (guid != Guid.Empty)
			{
				Console.WriteLine("Found official GUID {0} for part of speech {1}", guid, name);
				IPartOfSpeech result;
				if (_posRepo.TryGetObject(guid, out result))
					return result;
				// Not found in FDO? Create it.
				// We have an "official" GUID, so we use it to create a PartOfSpeech
				IPartOfSpeech newPos = (item == null) ?
					CreateFromWellKnownGuid(guid) :
					CreateFromWellKnownItem(item); // The Create* functions also populate the PoS data.
				if (newPos == null)
				{
					// This really shouldn't happen. TODO: Log this instead of printing to console.
					Console.WriteLine("Error: Well-known GUID {0} for part of speech \"{1}\" was not found in GOLDEtic.xml data. " +
						"This really shouldn't happen", guid, name);
					// Fall back to custom name creation instead
					return CreateFromCustomName(name, foundWs);
				}
				return newPos;
			}
			else
			{
				// No "official" GUID, so this is a "custom" PartOfSpeech... and some (but not necessarily all) of its ancestors might be as well.
				var pos = CreateFromCustomName(name, foundWs);
				Console.WriteLine("Creating part of speech with GUID {0} and full name {3} for name {1} in ws {2}.", pos.Guid, name, foundWs, pos.NameHierarchyString);
				return pos;
			}
		}
	}
}

