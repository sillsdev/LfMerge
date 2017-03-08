// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalPartOfSpeechSource : CanonicalOptionListSource
	{
		public CanonicalPartOfSpeechSource()
			: base("GOLDEtic.xml", "item")
		{
		}

		public override void LoadCanonicalData()
		{
			LoadCanonicalData<CanonicalPartOfSpeechItem>();
		}
	}
}
