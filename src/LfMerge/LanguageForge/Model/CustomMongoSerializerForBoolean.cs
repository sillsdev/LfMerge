// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace LfMerge
{
	public class CustomMongoSerializerForBoolean : BooleanSerializer
	{
		public override bool Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
		{
			var bsonReader = context.Reader;

			var bsonType = bsonReader.GetCurrentBsonType();
			if (bsonType == BsonType.String)
			{
				string token = bsonReader.ReadString();
				switch (token.ToLowerInvariant())
				{
				case "false":
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

