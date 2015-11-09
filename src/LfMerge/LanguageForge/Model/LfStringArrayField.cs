// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringArrayField : LfFieldBase
	{
		public List<string> Values { get; set; }

		public LfStringArrayField()
		{
			Values = new List<string>();
		}
	}
}

