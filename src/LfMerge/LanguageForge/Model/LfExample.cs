// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfExample : LfFieldBase
	{
		// Metadata properties
		[BsonElement("id")]
		public string ExampleId { get; set; } // Can't call this field "Id", or Mongo thinks it should be an ObjectId
		public string LiftId { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }

		// Data properties
		public LfAuthorInfo AuthorInfo { get; set; }
		public LfMultiText Sentence { get; set; }
		public LfMultiText Translation { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid TranslationGuid { get; set; }
		public LfMultiText Reference { get; set; }
		public LfStringArrayField ExamplePublishIn { get; set; }
		public BsonDocument CustomFields { get; set; }
		public BsonDocument CustomFieldGuids { get; set; }

		// Ugh. But Mongo doesn't let you provide a ShouldSerialize() by field *type*, only by field *name*.
		// Maybe later we can write reflection code to automatically add these to the class...
		// public bool ShouldSerializeAuthorInfo() { return true; } // Not needed, as this is the default
		public bool ShouldSerializeSentence() { return _ShouldSerializeLfMultiText(Sentence); }
		public bool ShouldSerializeTranslation() { return _ShouldSerializeLfMultiText(Translation); }
		public bool ShouldSerializeTranslationGuid() { return TranslationGuid != System.Guid.Empty; }
		public bool ShouldSerializeReference() { return _ShouldSerializeLfMultiText(Reference); }
		public bool ShouldSerializeExamplePublishIn() { return _ShouldSerializeLfStringArrayField(ExamplePublishIn); }
		public bool ShouldSerializeCustomFields() { return _ShouldSerializeList(CustomFields); }
		public bool ShouldSerializeCustomFieldGuids() { return _ShouldSerializeList(CustomFieldGuids); }
	}
}

