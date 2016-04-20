// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfParagraph : LfFieldBase, IHasNullableGuid
	{
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }
		public string StyleName { get; set; }
		public string Contents { get; set; }

		public bool ShouldSerializeGuid() { return (Guid != null && Guid.Value != System.Guid.Empty); }
		public bool ShouldSerializeStyleName() { return !String.IsNullOrEmpty(StyleName); }
		// Always serialize Contents even if empty. Thus, no ShouldSerializeContents() method needed.

		public bool IsEmpty { get { return String.IsNullOrEmpty(Contents); } }
	}
}

