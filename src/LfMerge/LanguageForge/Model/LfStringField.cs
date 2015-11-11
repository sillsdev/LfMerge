// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringField : LfFieldBase
	{
		public string Value { get; set; }

		public override string ToString()
		{
			return Value;
			// return string.Format("[LfStringField: Value={0}]", Value);
		}

		public static LfStringField FromString(string source)
		{
			return new LfStringField { Value = source };
		}
	}
}

