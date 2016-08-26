// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.Core.MongoConnector;

namespace LfMerge.Core.LanguageForge.Config
{
	public class MongoRegistrarForLfConfig : MongoRegistrar
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

