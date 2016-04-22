// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Infrastructure
{
	public class CustomFieldSpec
	{
		public CustomFieldSpec(string _name, string _specType)
		{
			name = _name;
			specType = _specType;
		}

		public string name { get; set; }
		public string specType { get; set; }
	}
}

