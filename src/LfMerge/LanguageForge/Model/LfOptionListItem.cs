// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;

namespace LfMerge.LanguageForge.Model
{
	public class LfOptionListItem
	{
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }
		public string Key { get; set; }
		public string Value { get; set; }
		public string Abbreviation { get; set; }
	}
}

