// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters.CanonicalSources
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
