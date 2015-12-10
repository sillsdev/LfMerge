// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public /*static*/ class PartOfSpeechConverter
	{
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

		public static IPartOfSpeech FromName(string name, IPartOfSpeechRepository repo)
		{
			string guidStr;
			Guid guid = Guid.Empty;
			if (PartOfSpeechMasterList.HierarchicalPoSGuids.TryGetValue(name, out guidStr))
			{
			}
			else if (PartOfSpeechMasterList.FlatPoSGuids.TryGetValue(name, out guidStr))
			{
			}
			else
			{
				// TODO: Parse the GOLDEtic.xml file and cache it, then look up in different languages.
			}
			if (guidStr != null)
				Guid.TryParse(guidStr, out guid);
			if (guid != Guid.Empty)
			{
				IPartOfSpeech result;
				if (repo.TryGetObject(guid, out result))
					return result;
				// TODO: Create new part of speech that's not yet registered, instead of just giving up and returning null.
				return null;
			}
			// TODO: Create new part of speech that's not yet registered, instead of just giving up and returning null.
			return null;
		}
	}
}

