// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;

namespace LfMerge.LanguageForge.Config
{
	public interface ILfProjectConfig
	{
		ObjectId Id { get; set; }
		BsonDocument Tasks { get; set; }
		LfConfigFieldList Entry { get; set; }
		BsonDocument RoleViews { get; set; }
		BsonDocument UserViews { get; set; }

	}
}

