// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using LfMerge.Core.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfFieldBase: ISupportInitialize
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

		// Will receive all additional fields from Mongo that we don't know about
		[BsonExtraElements]
		public IDictionary<string, object> ExtraElements { get; set; }

		void ISupportInitialize.BeginInit()
		{
			// nothing to do at beginning
		}

		void ISupportInitialize.EndInit()
		{
			if (ExtraElements == null || ExtraElements.Count <= 0)
				return;

			var bldr = new StringBuilder();
			bldr.AppendFormat("Read {0} unknown elements from Mongo for type {1}: ",
				ExtraElements.Count, GetType().Name);
			bool firstKey = true;
			foreach (var key in ExtraElements.Keys)
			{
				if (!firstKey)
					bldr.Append(", ");
				bldr.AppendFormat("{0}", key);
				firstKey = false;
			}
			MainClass.Logger.Log(LogSeverity.Warning, bldr.ToString());
		}
	}
}

