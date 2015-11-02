// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigFieldList : LfConfigFieldBase
	{
		// public override string TypeName { get { return LfConfigFieldTypeNames.FieldList; } }

		public List<string> FieldOrder;
		public Dictionary<string, LfConfigFieldBase> Fields;
		
		public LfConfigFieldList()
		{
			Fields = new Dictionary<string, LfConfigFieldBase>(); // TODO: Check if Mongo is populating this correctly with objects
		}

		public string GetFieldTypeName(string fieldName)
		{
			return Fields[fieldName].Type; // No TryGetValue here; we WANT an exception. TODO: Convert to TryGetValue after we're reasonably stable
		}
	}
}

