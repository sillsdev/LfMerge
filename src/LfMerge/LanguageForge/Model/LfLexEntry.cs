// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfLexEntry : LfFieldBase
	{
		// Metadata properties
		public ObjectId Id { get; set; }
		public string Guid { get; set; }
		public bool IsDeleted { get; set; }
		public string MercurialSha { get; set; }
		public DateTime DateCreated { get; set; }
		public DateTime DateModified { get; set; }

		// Data properties
		public LfMultiText Lexeme { get; set; }
		public LfSense[] Senses { get; set; }
		public LfAuthorInfo AuthorInfo { get; set; }
		public LfMultiText CitationForm { get; set; }
		public BsonDocument CustomFields { get; set; }
		public LfMultiText CvPattern { get; set; }
		public LfMultiText EntryBibliography { get; set; }
		public LfMultiText EntryRestrictions { get; set; }
		public BsonDocument Environments { get; set; }
		public LfMultiText Etymology { get; set; }
		public LfMultiText EtymologyGloss { get; set; }
		public LfMultiText EtymologyComment { get; set; }
		public LfMultiText EtymologySource { get; set; }
		public LfMultiText LiteralMeaning { get; set; }
		public LfStringField Location { get; set; }
		public string MorphologyType { get; set; }
		public LfMultiText Note { get; set; }
		public LfMultiText Pronunciation { get; set; }
		public LfMultiText SummaryDefinition { get; set; }
		public LfMultiText Tone { get; set; }
	}
}

