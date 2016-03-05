// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson.Serialization;
using LfMerge.LanguageForge.Config;
using LfMerge.MongoConnector;

namespace LfMerge.LanguageForge.Model
{
	public class MongoRegistrarForLfFields : MongoRegistrar
	{
		public MongoRegistrarForLfFields() :
		base(new LfConfigFieldTypeMapper())
		{
		}
	}
}

