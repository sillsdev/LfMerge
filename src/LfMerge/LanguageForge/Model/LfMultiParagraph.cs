// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Model
{
	public class LfMultiParagraph : LfFieldBase
	{
		public Guid? Guid { get; set; }
		public string InputSystem { get; set; }
		public List<LfParagraph> Paragraphs { get; set; }

		public bool ShouldSerializeGuid() { return (Guid != null && Guid.Value != System.Guid.Empty); }
		public bool ShouldSerializeInputSystem() { return !String.IsNullOrEmpty(InputSystem); }
		public bool ShouldSerializeParagraphs() { return (Paragraphs != null && Paragraphs.Count > 0); }

		public LfMultiParagraph() {
			Paragraphs = new List<LfParagraph>();
		}
	}
}

