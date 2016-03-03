// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using LfMerge.LanguageForge.Model;

namespace LfMerge.DataConverters
{
	public class GrammarConverter : ConvertOptionList
	{
		public GrammarConverter(LfOptionList lfOptionList, int wsForKeys) : base(lfOptionList, wsForKeys, MagicStrings.LfOptionListCodeForGrammaticalInfo)
		{
		}
	}
}
