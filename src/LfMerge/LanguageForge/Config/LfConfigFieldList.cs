// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigFieldList : LfConfigFieldBase
	{
		public List<string> FieldOrder;
		public Dictionary<string, LfConfigFieldBase> Fields;
		
		public LfConfigFieldList()
		{
			Fields = new Dictionary<string, LfConfigFieldBase>(); // TODO: Check if Mongo is populating this correctly with objects
		}
	}
}

