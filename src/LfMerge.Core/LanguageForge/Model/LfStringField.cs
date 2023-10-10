// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfStringField : LfFieldBase
	{
		public string Value { get; private set; }

		public bool IsEmpty { get { return String.IsNullOrEmpty(Value); } }

		public override string ToString()
		{
			return Value;
		}

		public static LfStringField CreateFrom(string source)
		{
			if (source == null)
				return null;
			return new LfStringField { Value = source };
		}

		private LfStringField() {	}

		public Dictionary<string, string> AsDictionary()
		{
			return new Dictionary<string, string> { { "value", Value } };
		}
	}
}

