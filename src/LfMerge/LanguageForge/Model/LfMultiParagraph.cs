// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Model
{
	public class LfMultiParagraph : LfFieldBase
	{
		public string Ws { get; set; }
		public List<LfParagraph> Paras { get; set; }

		public bool ShouldSerializeWs() { return !String.IsNullOrEmpty(Ws); }
		public bool ShouldSerializeParas() { return (Paras != null && Paras.Count > 0); }

		public LfMultiParagraph() {
			Paras = new List<LfParagraph>();
		}
	}
}

