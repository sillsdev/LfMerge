// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigMultiText : LfConfigFieldBase
	{
		public string Label { get; set; }
		public int Width { get; set; }
		public List<string> InputSystems { get; set; }
		public bool DisplayMultiline { get; set; }

		public LfConfigMultiText()
		{
			Label = String.Empty;
			DisplayMultiline = false;
			Width = 20;
			InputSystems = new List<string>();
		}
	}
}

