// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalLfTagSource : CanonicalOptionListSource
	{
		public CanonicalLfTagSource()
			: base("canonical-lf-tags.xml", "item")
		{
		}

		public override void LoadCanonicalData()
		{
			LoadCanonicalData<CanonicalLfTagItem>();
		}
	}
}
