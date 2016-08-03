// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.LanguageForge.Config
{
	public class LfConfigPictures : LfConfigMultiText
	{
		public string CaptionLabel { get; set; }
		public bool CaptionHideIfEmpty { get; set; }

		public LfConfigPictures() : base()
		{
			Label = "Pictures";
			CaptionLabel = "Captions";
			CaptionHideIfEmpty = true;
		}
	}
}

