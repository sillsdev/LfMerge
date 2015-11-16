// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Model
{
	public class LfPicture : LfFieldBase
	{
		public string FileName { get; set; }
		public LfMultiText Caption { get; set; }
		public Guid Guid { get; set; }
	}
}

