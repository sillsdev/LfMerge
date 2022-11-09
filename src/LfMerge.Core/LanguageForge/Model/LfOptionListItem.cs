// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;

namespace LfMerge.Core.LanguageForge.Model
{
  public class LfOptionListItem : IHasNullableGuid
  {
		private string key;

		[BsonRepresentation(BsonType.String)]
		public Guid? Guid { get; set; }
		public string Key
		{
			get => key;
			set
			{
				if (System.Guid.TryParse(value, out var guid))
				{
					key = guid.ToString();
				}
				else if (Guid.HasValue)
				{
					key = Guid.Value.ToString();
				}
				else
				{
					throw new ApplicationException("Cannot set Key property to non-GUID value " + value);
				}
			}
		}
		public string Value { get; set; }
		public string Abbreviation { get; set; }
	}
}

