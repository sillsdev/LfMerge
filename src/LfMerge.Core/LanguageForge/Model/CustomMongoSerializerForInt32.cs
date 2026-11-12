// Copyright (c) 2026 SIL Global
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace LfMerge.Core.LanguageForge.Model
{
	public class CustomMongoSerializerForInt32 : Int32Serializer
	{
		public override int Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
		{
			IBsonReader bsonReader = context.Reader;
			if (bsonReader.GetCurrentBsonType() == BsonType.Null)
			{
				bsonReader.ReadNull();
				return 0;
			}
			return base.Deserialize(context, args);
		}
	}
}