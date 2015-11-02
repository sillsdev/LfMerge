// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigFieldTypeMapper : LfFieldTypeMapper
	{
		public LfConfigFieldTypeMapper()
			: base(
				new BidirectionalDictionary<string, Type> {
					{ LfConfigFieldTypeNames.FieldList, typeof(LfConfigFieldList) },
					{ LfConfigFieldTypeNames.MultiText, typeof(LfConfigMultiText) },
					{ LfConfigFieldTypeNames.OptionList, typeof(LfConfigOptionList) },
					{ LfConfigFieldTypeNames.MultiOptionList, typeof(LfConfigMultiOptionList) },
					{ LfConfigFieldTypeNames.Pictures, typeof(LfConfigPictures) }
				})
		{
		}
	}
}

