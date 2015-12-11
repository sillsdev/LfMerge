// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public class PartOfSpeechConverter
	{
		public FdoCache cache;
		private IPartOfSpeechRepository posRepo;
		private IPartOfSpeechFactory posFactory;

		public PartOfSpeechConverter(FdoCache fdoCache)
		{
			cache = fdoCache;
			posRepo = cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>();
			posFactory = cache.ServiceLocator.GetInstance<IPartOfSpeechFactory>();
		}

		public static Lazy<Stream> GoldEticXml = new Lazy<Stream>(() =>
			typeof(PartOfSpeechConverter).Assembly.GetManifestResourceStream(typeof(PartOfSpeechConverter), "GOLDEtic.xml")
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

		public static string FromGuid(Guid guid, bool flat=false)
		{
			return FromGuid(guid.ToString(), flat);
		}

		public static string FromGuid(string guid, bool flat=false)
		{
			string result;
			Dictionary<string, string> lookupTable = (flat) ?
				PartOfSpeechMasterList.FlatPoSGuids :
				PartOfSpeechMasterList.HierarchicalPoSGuids;
			if (lookupTable.TryGetValue(guid, out result))
				return result;
			// This GUID not found? Hmmm, try something else maybe.
			// TODO: Implement "get name instead" code. Maybe in a different function, though.
			// TODO: Actually, just fold this function into the other one since this won't ever need to be called separately.
			return null;
		}

		public static IPartOfSpeech FromMSA(IMoMorphSynAnalysis msa)
		{
			switch (msa.GetType().Name)
			{
			case "MoDerivAffMsa":
				// TODO: Turn this into a log message, and try to make the log message a little clearer to non-linguists, if possible.
				Console.WriteLine("For derivational affix {0}, arbitrarily picking \"To\" part of speech instead of the \"From\" part of speech.", msa.GetGlossOfFirstSense());
				return ((IMoDerivAffMsa)msa).ToPartOfSpeechRA;
			case "MoDerivStepMsa":
				return ((IMoDerivStepMsa)msa).PartOfSpeechRA;
			case "MoInflAffMsa":
				return ((IMoInflAffMsa)msa).PartOfSpeechRA;
			case "MoStemMsa":
				return ((IMoStemMsa)msa).PartOfSpeechRA;
			case "MoUnclassifiedAffixMsa":
				return ((IMoUnclassifiedAffixMsa)msa).PartOfSpeechRA;
			default:
				// TODO: Make this a log message, not Console.WriteLine
				Console.WriteLine("Got MSA of unknown type {0}", msa.GetType().Name);
				return null;
			}
		}

		public static void SetPartOfSpeech(IMoMorphSynAnalysis msa, IPartOfSpeech pos)
		{
			switch (msa.GetType().Name)
			{
			case "MoDerivAffMsa":
				// TODO: Turn this into a log message, and try to make the log message a little clearer to non-linguists, if possible.
				Console.WriteLine("For derivational affix {0}, arbitrarily picking \"To\" part of speech instead of the \"From\" part of speech.", msa.GetGlossOfFirstSense());
				((IMoDerivAffMsa)msa).ToPartOfSpeechRA = pos;
				break;
			case "MoDerivStepMsa":
				((IMoDerivStepMsa)msa).PartOfSpeechRA = pos;
				break;
			case "MoInflAffMsa":
				((IMoInflAffMsa)msa).PartOfSpeechRA = pos;
				break;
			case "MoStemMsa":
				((IMoStemMsa)msa).PartOfSpeechRA = pos;
				break;
			case "MoUnclassifiedAffixMsa":
				((IMoUnclassifiedAffixMsa)msa).PartOfSpeechRA = pos;
				break;
			default:
				// TODO: Make this a log message, not Console.WriteLine
				Console.WriteLine("Got MSA of unknown type {0}", msa.GetType().Name);
				return;
			}
		}

		private string FindGuidInGoldEtic(string searchTerm, string wsToSearch)
		{
			foreach (GoldEticItem item in FlattenedGoldEticItems.Value)
				if (item.ORCDelimitedNameByWs(wsToSearch) == searchTerm || item.NameByWs(wsToSearch) == searchTerm)
					return item.Guid;
			return null;
		}

		public IPartOfSpeech FromName(string name, string wsToSearch = "en", string fallbackWs = "en")
		{
			string guidStr;
			Guid guid = Guid.Empty;
			PartOfSpeechMasterList.HierarchicalPoSGuids.TryGetValue(name, out guidStr);
			if (guidStr == null)
				PartOfSpeechMasterList.FlatPoSGuids.TryGetValue(name, out guidStr);
			if (guidStr == null)
				guidStr = FindGuidInGoldEtic(name, wsToSearch);
			if (guidStr == null)
				guidStr = FindGuidInGoldEtic(name, fallbackWs);
			if (guidStr != null)
				Guid.TryParse(guidStr, out guid);
			if (guid != Guid.Empty)
			{
				IPartOfSpeech result;
				if (posRepo.TryGetObject(guid, out result))
					return result;
				// Not found? Fall through to creation.
			}
			// Whether or not we have an "official" GUID, we can now create a PartOfSpeech
			return posFactory.Create(guid, cache.LanguageProject.PartsOfSpeechOA);
		}
	}
}

