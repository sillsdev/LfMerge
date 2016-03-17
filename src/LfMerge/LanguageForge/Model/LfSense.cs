// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfSense : LfFieldBase, IHasNullableGuid
	{
		// Metadata properties
		[BsonElement("id")]
		public string SenseId { get; set; } // Can't call this field "Id", or Mongo thinks it should be an ObjectId
		public string LiftId { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }

		// Data properties
		public LfStringField PartOfSpeech { get; set; }
		public LfStringField SecondaryPartOfSpeech { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid? PartOfSpeechGuid { get; set; } // TODO: Delete this since it *should* now be unused. (Test first, though)
		public LfStringArrayField SemanticDomain { get; set; }
		public List<LfExample> Examples { get; set; }
		public BsonDocument CustomFields { get; set; } // Mapped at runtime
		public BsonDocument CustomFieldGuids { get; set; }
		public LfAuthorInfo AuthorInfo { get; set; }
		public List<LfPicture> Pictures { get; set; }
		public LfMultiText Definition { get; set; }
		public LfMultiText Gloss { get; set; }
		public LfMultiText ScientificName { get; set; }
		public LfMultiText AnthropologyNote { get; set; }
		public LfMultiText SenseBibliography { get; set; }
		public LfMultiText DiscourseNote { get; set; }
		public LfMultiText EncyclopedicNote { get; set; }
		public LfMultiText GeneralNote { get; set; }
		public LfMultiText GrammarNote { get; set; }
		public LfMultiText PhonologyNote { get; set; }
		public LfMultiText SenseRestrictions { get; set; }
		public LfMultiText SemanticsNote { get; set; }
		public LfMultiText SociolinguisticsNote { get; set; }
		public LfMultiText Source { get; set; }
		public LfMultiText SenseImportResidue { get; set; }
		public LfStringArrayField Usages { get; set; }
		public LfStringArrayField ReversalEntries { get; set; }
		public LfStringField SenseType { get; set; }
		public LfStringArrayField AcademicDomains { get; set; }
		public LfStringArrayField SensePublishIn { get; set; }
		public LfStringArrayField AnthropologyCategories { get; set; }
		public LfStringArrayField Status { get; set; }

		// Ugh. But Mongo doesn't let you provide a ShouldSerialize() by field *type*, only by field *name*.
		// Maybe later we can write reflection code to automatically add these to the class...
		public bool ShouldSerializePartOfSpeech() { return _ShouldSerializeLfStringField(PartOfSpeech); }
		public bool ShouldSerializeSecondaryPartOfSpeech() { return _ShouldSerializeLfStringField(SecondaryPartOfSpeech); }
		public bool ShouldSerializeSemanticDomain() { return _ShouldSerializeLfStringArrayField(SemanticDomain); }
		public bool ShouldSerializeExamples() { return _ShouldSerializeList(Examples); }
		public bool ShouldSerializeCustomFields() { return _ShouldSerializeBsonDocument(CustomFields); }
		public bool ShouldSerializeCustomFieldGuids() { return _ShouldSerializeBsonDocument(CustomFieldGuids); }
		public bool ShouldSerializeAuthorInfo() { return AuthorInfo != null; }
		public bool ShouldSerializePictures() { return _ShouldSerializeList(Pictures); }
		public bool ShouldSerializeDefinition() { return _ShouldSerializeLfMultiText(Definition); }
		public bool ShouldSerializeGloss() { return _ShouldSerializeLfMultiText(Gloss); }
		public bool ShouldSerializeScientificName() { return _ShouldSerializeLfMultiText(ScientificName); }
		public bool ShouldSerializeAnthropologyNote() { return _ShouldSerializeLfMultiText(AnthropologyNote); }
		public bool ShouldSerializeSenseBibliography() { return _ShouldSerializeLfMultiText(SenseBibliography); }
		public bool ShouldSerializeDiscourseNote() { return _ShouldSerializeLfMultiText(DiscourseNote); }
		public bool ShouldSerializeEncyclopedicNote() { return _ShouldSerializeLfMultiText(EncyclopedicNote); }
		public bool ShouldSerializeGeneralNote() { return _ShouldSerializeLfMultiText(GeneralNote); }
		public bool ShouldSerializeGrammarNote() { return _ShouldSerializeLfMultiText(GrammarNote); }
		public bool ShouldSerializePhonologyNote() { return _ShouldSerializeLfMultiText(PhonologyNote); }
		public bool ShouldSerializeSenseRestrictions() { return _ShouldSerializeLfMultiText(SenseRestrictions); }
		public bool ShouldSerializeSemanticsNote() { return _ShouldSerializeLfMultiText(SemanticsNote); }
		public bool ShouldSerializeSociolinguisticsNote() { return _ShouldSerializeLfMultiText(SociolinguisticsNote); }
		public bool ShouldSerializeSource() { return _ShouldSerializeLfMultiText(Source); }
		public bool ShouldSerializeSenseImportResidue() { return _ShouldSerializeLfMultiText(SenseImportResidue); }
		public bool ShouldSerializeUsages() { return _ShouldSerializeLfStringArrayField(Usages); }
		public bool ShouldSerializeReversalEntries() { return _ShouldSerializeLfStringArrayField(ReversalEntries); }
		public bool ShouldSerializeSenseType() { return _ShouldSerializeLfStringField(SenseType); }
		public bool ShouldSerializeAcademicDomains() { return _ShouldSerializeLfStringArrayField(AcademicDomains); }
		public bool ShouldSerializeSensePublishIn() { return _ShouldSerializeLfStringArrayField(SensePublishIn); }
		public bool ShouldSerializeAnthropologyCategories() { return _ShouldSerializeLfStringArrayField(AnthropologyCategories); }
		public bool ShouldSerializeStatus() { return _ShouldSerializeLfStringArrayField(Status); }

		public LfSense()
		{
			Examples = new List<LfExample>();
			Pictures = new List<LfPicture>();
		}
	}
}
