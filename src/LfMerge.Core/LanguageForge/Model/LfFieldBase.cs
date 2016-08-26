// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfFieldBase
	{
		// Used in subclasses to help reduce size of MongoDB JSON serializations
		protected bool _ShouldSerializeLfMultiText(LfMultiText value)
		{
			return value != null && !value.IsEmpty && value.Values.AsEnumerable().All(field => field != null && !field.IsEmpty);
		}

		protected bool _ShouldSerializeLfStringArrayField(LfStringArrayField value)
		{
			return value != null && !value.IsEmpty && value.Values.TrueForAll(s => !string.IsNullOrEmpty(s));
		}

		protected bool _ShouldSerializeLfStringField(LfStringField value)
		{
			return value != null && !value.IsEmpty;
		}

		protected bool _ShouldSerializeList(IEnumerable<object> value)
		{
			return value != null && value.GetEnumerator().MoveNext() != false;
		}

		protected bool _ShouldSerializeBsonDocument(BsonDocument value)
		{
			return value != null && value.ElementCount > 0;
		}
	}
}

