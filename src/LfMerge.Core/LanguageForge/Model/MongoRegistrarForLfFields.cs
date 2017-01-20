// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.MongoConnector;

namespace LfMerge.Core.LanguageForge.Model
{
	public class MongoRegistrarForLfFields : MongoRegistrar
	{
		public MongoRegistrarForLfFields() :
		base(new LfConfigFieldTypeMapper())
		{
		}
	}
}

