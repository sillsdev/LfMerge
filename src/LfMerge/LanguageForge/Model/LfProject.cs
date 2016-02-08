// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	[BsonIgnoreExtraElements]
	public class LfProject
	{
		public ObjectId Id { get; set; }
		public string ProjectCode { get; set; }
		public string ProjectName { get; set; }
		public List<LfInputSystemRecord> InputSystems { get; set; }

		public LfProject()
		{
			InputSystems = new List<LfInputSystemRecord>();
		}
	}
}

