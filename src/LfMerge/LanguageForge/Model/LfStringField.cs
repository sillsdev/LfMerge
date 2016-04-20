// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public class LfStringField : LfFieldBase
	{
		public string Value { get; set; }

		[BsonRepresentation(BsonType.String)]  // Yes, this works with List<Guid>. Very nice.
		public List<Guid> Guids { get; set; }  // Used only for custom MultiPara fields. Empty or missing otherwise.
		public bool ShouldSerializeGuids() { return (Guids != null) && (Guids.Count > 0); }

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
			return new LfStringField { Value = source, Guids = new List<Guid>() };
		}

		public LfStringField()
		{
			Guids = new List<Guid>();
		}

		public Dictionary<string, string> AsDictionary()
		{
			return new Dictionary<string, string> { { "value", Value } };
		}
	}
}

