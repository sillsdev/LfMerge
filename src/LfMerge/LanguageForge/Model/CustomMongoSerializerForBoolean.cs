// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace LfMerge.LanguageForge.Model
{
	public static class ParseBoolean
	{
		public static bool FromString(string s)
		{
			switch (s.ToLowerInvariant())
			{
			case "false":
			case "off":
			case "no":
			case "f":
			case "n":
			case "0":
			case "": // Also consider the empty string to be false
				return false;
			default:
				return true; // Non-empty strings are true unless they are a specifically "false-like" value
			}
		}
	}

	public class CustomMongoSerializerForBoolean : BooleanSerializer
	{
		public override bool Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
		{
			IBsonReader bsonReader = context.Reader;

			BsonType bsonType = bsonReader.GetCurrentBsonType();
			if (bsonType == BsonType.String)
			{
				string token = bsonReader.ReadString();
				return ParseBoolean.FromString(token);
			}
			else
			{
				return base.Deserialize(context, args);
			}
		}
	}

	public class BooleanSerializationProvider : IBsonSerializationProvider
	{
		public IBsonSerializer GetSerializer(Type type)
		{
			if (type == typeof(bool))
			{
				return new CustomMongoSerializerForBoolean();
			}
			return null;
		}
	}
}

