// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringField : LfFieldBase
	{
		public string Value { get; set; }

		public bool IsEmpty { get { return String.IsNullOrEmpty(Value); } }

		public override string ToString()
		{
			return Value;
			// return string.Format("[LfStringField: Value={0}]", Value);
		}

		public static LfStringField FromString(string source)
		{
			if (source == null)
				return null;
			return new LfStringField { Value = source };
		}

		public Dictionary<string, string> AsDictionary()
		{
			return new Dictionary<string, string> { { "value", Value } };
		}
	}
}

