// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Infrastructure
{
	public class CustomFieldSpec
	{
		public CustomFieldSpec(string _fieldName, string _fieldType)
		{
			fieldName = _fieldName;
			fieldType = _fieldType;
		}

		// note property names are camelCase to match those expected in PHP code
		public string fieldName { get; set; }
		public string fieldType { get; set; }
	}
}

