// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfCommentRegarding : LfFieldBase
	{
		public string TargetGuid { get; set; }
		public string Field { get; set; }
		public string FieldNameForDisplay { get; set; }
		public string FieldValue { get; set; }
		public string InputSystem { get; set; }
		public string InputSystemAbbreviation { get; set; }
		public string Word { get; set; }
		public string Meaning { get; set; }

		public bool ShouldSerializeEntryGuid() { return ( ! String.IsNullOrEmpty(TargetGuid)); }
		public bool ShouldSerializeField() { return ( ! String.IsNullOrEmpty(Field)); }
		public bool ShouldSerializeFieldNameForDisplay() { return ( ! String.IsNullOrEmpty(FieldNameForDisplay)); }
		public bool ShouldSerializeFieldValue() { return ( ! String.IsNullOrEmpty(FieldValue)); }
		public bool ShouldSerializeInputSystem() { return ( ! String.IsNullOrEmpty(InputSystem)); }
		public bool ShouldSerializeInputSystemAbbreviation() { return ( ! String.IsNullOrEmpty(InputSystemAbbreviation)); }
		public bool ShouldSerializeWord() { return ( ! String.IsNullOrEmpty(Word)); }
		public bool ShouldSerializeMeaning() { return ( ! String.IsNullOrEmpty(Meaning)); }
	}
}

