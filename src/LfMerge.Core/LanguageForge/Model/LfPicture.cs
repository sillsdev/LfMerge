// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfPicture : LfFieldBase, IHasNullableGuid
	{
		public string FileName { get; set; }
		public LfMultiText Caption { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }

		public bool ShouldSerializeCaption() { return _ShouldSerializeLfMultiText(Caption); }
		// TODO: Test this one. If it doesn't make things crash, use it. But if it turns out we NEED a Guid serialized
		// even if it's Guid.Empty, then don't uncomment this next line.
		// public bool ShouldSerializeGuid() { return Guid != null && Guid.Value != System.Guid.Empty; }
	}
}

