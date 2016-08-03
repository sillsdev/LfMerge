// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;

namespace LfMerge.LanguageForge.Config
{
	public abstract class LfConfigFieldBase
	{
		public string Label { get; set; }
		public bool HideIfEmpty { get; set; }

		public LfConfigFieldBase()
		{
			Label = string.Empty;
			HideIfEmpty = false; // TODO: Consider setting it to true here and overriding with false only on the common fields
		}
		// Derived classes must override this to be the appropriate string from LfConfigFieldTypeNames
		// public virtual string TypeName { get; set; }
	}
}

