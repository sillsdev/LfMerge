// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	[BsonIgnoreExtraElements]
	public class LfInputSystemRecord
	{
		public string Abbreviation { get; set; }
		public string Tag { get; set; }
		public string LanguageName { get; set; }
		public bool IsRightToLeft { get; set; }
	}
}
