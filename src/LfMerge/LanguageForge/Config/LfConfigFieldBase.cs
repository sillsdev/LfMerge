// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;

namespace LfMerge.LanguageForge.Config
{
	public abstract class LfConfigFieldBase
	{

		public ObjectId Id { get; set; }
		public string Type { get; set; }
		public bool HideIfEmpty { get; set; }

		// Derived classes must override this to be the appropriate string from LfConfigFieldTypeNames
		// public virtual string TypeName { get; set; }
	}
}

