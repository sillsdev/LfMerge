// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfLexEntry : LfFieldBase, IHasNullableGuid
	{
		// Metadata properties
		public ObjectId Id { get; set; }
		public string LiftId { get; set; } // TODO Investigate why this seems to not be modeled in LF PHP code... should it be?
		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }
		public bool IsDeleted { get; set; }
		public string MercurialSha { get; set; }
		public DateTime DateCreated { get; set; }
		public DateTime DateModified { get; set; }
		public int DirtySR { get; set ; }

		// Data properties
		public LfMultiText Lexeme { get; set; }
		public List<LfSense> Senses { get; set; }
		public LfAuthorInfo AuthorInfo { get; set; }
		public LfMultiText CitationForm { get; set; }
		public BsonDocument CustomFields { get; set; }
		public BsonDocument CustomFieldGuids { get; set; }
		public LfMultiText CvPattern { get; set; }
		public LfMultiText EntryBibliography { get; set; }
		public LfMultiText EntryRestrictions { get; set; }
		public LfStringArrayField Environments { get; set; }
		public LfMultiText Etymology { get; set; }
		public LfMultiText EtymologyGloss { get; set; }
		public LfMultiText EtymologyComment { get; set; }
		public LfMultiText EtymologySource { get; set; }
		public LfMultiText LiteralMeaning { get; set; }
		public LfStringField Location { get; set; }
		public string MorphologyType { get; set; }
		public LfMultiText Note { get; set; }
		public LfMultiText Pronunciation { get; set; }
		[BsonRepresentation(BsonType.String)]
		public Guid PronunciationGuid { get; set; }
		public LfMultiText SummaryDefinition { get; set; }
		public LfMultiText Tone { get; set; }

		public LfLexEntry()
		{
			Senses = new List<LfSense>();
		}

		// Ugh. But Mongo doesn't let you provide a ShouldSerialize() by field *type*, only by field *name*.
		// Maybe later we can write reflection code to automatically add these to the class...
		public bool ShouldSerializeLexeme() { return _ShouldSerializeLfMultiText(Lexeme); }
		public bool ShouldSerializeSenses() { return _ShouldSerializeList(Senses); }
		// public bool ShouldSerializeAuthorInfo() { return true; } // Not needed, as this is the default
		public bool ShouldSerializeCitationForm() { return _ShouldSerializeLfMultiText(CitationForm); }
		public bool ShouldSerializeCustomFields() { return _ShouldSerializeBsonDocument(CustomFields); }
		public bool ShouldSerializeCustomFieldGuids() { return _ShouldSerializeBsonDocument(CustomFieldGuids); }
		public bool ShouldSerializeCvPattern() { return _ShouldSerializeLfMultiText(CvPattern); }
		public bool ShouldSerializeEntryBibliography() { return _ShouldSerializeLfMultiText(EntryBibliography); }
		public bool ShouldSerializeEntryRestrictions() { return _ShouldSerializeLfMultiText(EntryRestrictions); }
		public bool ShouldSerializeEnvironments() { return _ShouldSerializeLfStringArrayField(Environments); }
		public bool ShouldSerializeEtymology() { return _ShouldSerializeLfMultiText(Etymology); }
		public bool ShouldSerializeEtymologyGloss() { return _ShouldSerializeLfMultiText(EtymologyGloss); }
		public bool ShouldSerializeEtymologyComment() { return _ShouldSerializeLfMultiText(EtymologyComment); }
		public bool ShouldSerializeEtymologySource() { return _ShouldSerializeLfMultiText(EtymologySource); }
		public bool ShouldSerializeLiteralMeaning() { return _ShouldSerializeLfMultiText(LiteralMeaning); }
		public bool ShouldSerializeLocation() { return _ShouldSerializeLfStringField(Location); }
		public bool ShouldSerializeMorphologyType() { return !String.IsNullOrEmpty(MorphologyType); }
		public bool ShouldSerializeNote() { return _ShouldSerializeLfMultiText(Note); }
		public bool ShouldSerializePronunciation() { return _ShouldSerializeLfMultiText(Pronunciation); }
		public bool ShouldSerializePronunciationGuid() { return PronunciationGuid != System.Guid.Empty; }
		public bool ShouldSerializeSummaryDefinition() { return _ShouldSerializeLfMultiText(SummaryDefinition); }
		public bool ShouldSerializeTone() { return _ShouldSerializeLfMultiText(Tone); }
	}
}
