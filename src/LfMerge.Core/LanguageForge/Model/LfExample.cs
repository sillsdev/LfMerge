// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using LfMergeBridge.LfMergeModel;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfExample : LfFieldBase, IHasNullableGuid
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
		public bool ShouldSerializeAuthorInfo() { return (AuthorInfo != null); }
		public bool ShouldSerializeSentence() { return _ShouldSerializeLfMultiText(Sentence); }
		public bool ShouldSerializeTranslation() { return _ShouldSerializeLfMultiText(Translation); }
		public bool ShouldSerializeTranslationGuid() { return TranslationGuid != System.Guid.Empty; }
		public bool ShouldSerializeReference() { return _ShouldSerializeLfMultiText(Reference); }
		public bool ShouldSerializeExamplePublishIn() { return false; }  // Get rid of this one if we find it
		public bool ShouldSerializeCustomFields() { return _ShouldSerializeBsonDocument(CustomFields); }
		public bool ShouldSerializeCustomFieldGuids() { return _ShouldSerializeBsonDocument(CustomFieldGuids); }
	}
}

