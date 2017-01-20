// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LfMerge.Core.LanguageForge.Model
{
	[BsonIgnoreExtraElements] // WARNING: Beware of using FindOneAndReplace() with IgnoreExtraElements, as you can lose data
	public class LfProject
	{
		public ObjectId Id { get; set; }
		public string ProjectCode { get; set; }
		public string ProjectName { get; set; }
		public Dictionary<string, LfInputSystemRecord> InputSystems { get; set; }

		public LfProject()
		{
			InputSystems = new Dictionary<string, LfInputSystemRecord>();
		}
	}
}

