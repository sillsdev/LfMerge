// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.Core.Logging;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToFdoPartsOfSpeech
	{
		// This class used to contain code for parsing the canonical part-of-speech data from GOLDEtic.xml and creating
		// new IPartOfSpeech objects from the canonical data if they were in Mongo but not in FDO. Most of that code has
		// been moved to the classes in the CanonicalSources namespace, and the "create new IPartOfSpeech" objects has
		// been moved to ConvertMongoToFdoOptionList (and made more generic). The only thing left in this class is the
		// static SetPartOfSpeech function, which deals with the complexities of MSAs.

		public static void SetPartOfSpeech(IMoMorphSynAnalysis msa, IPartOfSpeech pos, IPartOfSpeech secondaryPos = null, ILogger logger = null)
		{
			if (msa == null)
			{
				if (logger != null)
					logger.Debug("Trying to set part of speech \"{0}\" in MSA, but MSA was null", pos == null ? "(null)" : pos.AbbrAndName);
				return;
			}
			if (pos == null)
			{
				if (logger != null)
					logger.Debug("Trying to set a null part of speech in MSA \"{0}\" with GUID {1}", msa.GetGlossOfFirstSense(), msa.Guid);
				return;
			}
			switch (msa.ClassID)
			{
			case MoDerivAffMsaTags.kClassId:
				((IMoDerivAffMsa)msa).FromPartOfSpeechRA = pos;
				// It's OK for secondaryPos to be null here; this represents *removing* the "To" part of speech link from this MSA
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
				// We'll only reach here if new MSA types are added to FDO and we forget to update the switch statement above
				if (logger != null)
					logger.Debug("Got MSA of unknown type {0}", msa.GetType().Name);
				return;
			}
		}
	}
}

