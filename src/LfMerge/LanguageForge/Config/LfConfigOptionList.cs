// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigOptionList : LfConfigFieldBase
	{
		public string ListCode { get; set;}

		public LfConfigOptionList() : base()
		{
			ListCode = String.Empty;
			HideIfEmpty = false;
		}
	}
}

