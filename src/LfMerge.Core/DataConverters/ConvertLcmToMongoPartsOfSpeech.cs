// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using SIL.LCModel;

namespace LfMerge.Core.DataConverters
{
	public class ConvertLcmToMongoPartsOfSpeech
	{
		public static int[] KnownMsaClassIds = {
			MoDerivAffMsaTags.kClassId,
			MoDerivStepMsaTags.kClassId,
			MoInflAffMsaTags.kClassId,
			MoStemMsaTags.kClassId,
			MoUnclassifiedAffixMsaTags.kClassId,
		};
		public static IPartOfSpeech FromMSA(IMoMorphSynAnalysis msa, out IPartOfSpeech secondaryPos)
		{
			secondaryPos = null;
			switch (msa.ClassID)
			{
			case MoDerivAffMsaTags.kClassId:
				// LCM considers the "From" PoS to be the main one, and "To" to be the secondary one
				secondaryPos = ((IMoDerivAffMsa)msa).ToPartOfSpeechRA;
				return ((IMoDerivAffMsa)msa).FromPartOfSpeechRA;
			case MoDerivStepMsaTags.kClassId:
				return ((IMoDerivStepMsa)msa).PartOfSpeechRA;
			case MoInflAffMsaTags.kClassId:
				return ((IMoInflAffMsa)msa).PartOfSpeechRA;
			case MoStemMsaTags.kClassId:
				return ((IMoStemMsa)msa).PartOfSpeechRA;
			case MoUnclassifiedAffixMsaTags.kClassId:
				return ((IMoUnclassifiedAffixMsa)msa).PartOfSpeechRA;
			default:
				return null;
			}
		}
	}
}

