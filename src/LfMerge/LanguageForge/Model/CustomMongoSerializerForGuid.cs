// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace LfMerge
{
	public class CustomMongoSerializerForGuid : GuidSerializer
	{
		public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
		{
			var bsonReader = context.Reader;

			var bsonType = bsonReader.GetCurrentBsonType();
			if (bsonType == BsonType.Null)
			{
//				Console.WriteLine("Reading a null GUID...");
				bsonReader.ReadNull(); // Need to consume the token
				return Guid.Empty;
			}
//			else if (bsonType == BsonType.String)
//			{
//				bsonReader.GetBookmark();
//			}
			else
			{
				return base.Deserialize(context, args);
			}
		}
	}

	public class GuidSerializationProvider : IBsonSerializationProvider
	{
		public IBsonSerializer GetSerializer(Type type)
		{
			if (type == typeof(Guid))
			{
				return new CustomMongoSerializerForGuid();
			}
			return null;
		}
	}
}

