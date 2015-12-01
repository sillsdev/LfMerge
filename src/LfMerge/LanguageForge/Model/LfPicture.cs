// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfPicture : LfFieldBase
	{
		public string FileName { get; set; }
		public LfMultiText Caption { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid Guid { get; set; }
	}
}

