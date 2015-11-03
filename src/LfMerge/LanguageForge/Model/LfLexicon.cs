// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.LanguageForge.Config;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfLexicon
	{
		private ILfProjectConfig Config { get; set; }

		public LfLexicon(ILfProjectConfig config)
		{
			Config = config;
		}

		public void PopulateFields(BsonDocument source)
		{
			foreach (KeyValuePair<string, LfConfigFieldBase> fieldKeyValue in Config.Entry.Fields)
			{
				string name = fieldKeyValue.Key;
				LfConfigFieldBase field = fieldKeyValue.Value;
				switch (field.Type)
				{
				case LfConfigFieldTypeNames.FieldList:
					break;
				}
			}
		}
	}
}

