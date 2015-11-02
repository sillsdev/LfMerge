// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson.Serialization;

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigPictures : LfConfigMultiText
	{
		// public string Label { get; set; } // Inherited from MultiText
		public string CaptionLabel { get; set; }
		public bool CaptionHideIfEmpty { get; set; }

		public LfConfigPictures()
		{
			Label = "Pictures";
			CaptionLabel = "Captions";
			CaptionHideIfEmpty = true;
		}
	}
}

