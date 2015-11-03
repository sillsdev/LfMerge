// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringArrayField : LfFieldBase, System.ComponentModel.ISupportInitialize
	{
		public string[] Values { get; set; }

		public void BeginInit() { }

		public void EndInit()
		{
			// Ensure Values is an array no matter what
			if (Values == null)
				Values = new string[0];
		}
	}
}

