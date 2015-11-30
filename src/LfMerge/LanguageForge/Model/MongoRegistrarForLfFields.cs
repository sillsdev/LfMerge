// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson.Serialization;
using LfMerge.LanguageForge.Config;

namespace LfMerge.LanguageForge.Model
{
	public class MongoRegistrarForLfFields : LfMerge.MongoRegistrar
	{
		public MongoRegistrarForLfFields() :
		base(new LfConfigFieldTypeMapper())
		{
		}

		public override void RegisterClassMappings()
		{
			// TODO: Actually not used. Can probably get rid of this class and just use automapping for LfFields.
			RegisterClassMapsForDerivedClassesOf(typeof(LfFieldBase));
		}
	}
}

