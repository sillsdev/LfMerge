// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
//using MongoDB.Bson;
//using MongoDB.Bson.Serialization.Attributes;

namespace LfMerge.LanguageForge.Model
{
	public interface IHasNullableGuid
	{
//		[BsonRepresentation(BsonType.String)]
		Guid? Guid { get; set; }
	}
}

