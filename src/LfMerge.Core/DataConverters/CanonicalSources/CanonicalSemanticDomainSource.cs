// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalSemanticDomainSource : CanonicalOptionListSource
	{
		public CanonicalSemanticDomainSource()
			: base("SemDom.xml", "CmSemanticDomain")
		{
		}

		public override void LoadCanonicalData()
		{
			LoadCanonicalData<CanonicalSemanticDomainItem>();
		}
	}
}
