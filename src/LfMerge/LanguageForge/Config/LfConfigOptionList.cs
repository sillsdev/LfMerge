// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigOptionList : LfConfigFieldBase
	{
		public string Label { get; set;}
		public string ListCode { get; set;}

		public LfConfigOptionList()
		{
			Label = String.Empty;
			ListCode = String.Empty;
		}
	}
}

