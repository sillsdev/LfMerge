// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;

namespace LfMerge.LanguageForge.Config
{
	public class LfProjectConfig : ILfProjectConfig
	{
		// At some point, we may convert the BsonDocument fields to properly-mapped classes,
		// but for now we don't need Tasks, RoleViews, or UserViews, so we'll just leave them
		// as unmapped BsonDocuments until we need them. TODO: Convert them if needed.
		public ObjectId Id { get; set; }
		public BsonDocument Tasks { get; set; }
		public LfConfigFieldList Entry { get; set; }
		public BsonDocument RoleViews { get; set; }
		public BsonDocument UserViews { get; set; }

		public LfProjectConfig()
		{
		}
	}
}

