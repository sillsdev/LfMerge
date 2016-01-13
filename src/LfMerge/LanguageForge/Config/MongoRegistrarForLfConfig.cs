// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.LanguageForge.Config
{
	public class MongoRegistrarForLfConfig : LfMerge.MongoRegistrar
	{
		public MongoRegistrarForLfConfig() :
			base(new LfConfigFieldTypeMapper())
		{
		}

		public override void RegisterClassMappings()
		{
			RegisterClassMapsForDerivedClassesOf(typeof(LfConfigFieldBase));
			RegisterClassIgnoreExtraFields(typeof(LfProjectConfig));
			RegisterClassIgnoreExtraFields(typeof(MongoProjectRecord));
		}
	}
}

