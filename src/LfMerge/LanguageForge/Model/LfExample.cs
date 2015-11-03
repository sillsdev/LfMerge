// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfExample : LfFieldBase
	{
		[BsonElement("id")]
		public string ExampleId { get; set; }
		public string LiftId { get; set; }
		public LfAuthorInfo AuthorInfo { get; set; }
		public LfMultiText Sentence { get; set; }
		public LfMultiText Translation { get; set; }
		public LfMultiText Reference { get; set; }
		public LfStringArrayField ExamplePublishIn { get; set; }
		public BsonDocument CustomFields { get; set; }
	}
}

