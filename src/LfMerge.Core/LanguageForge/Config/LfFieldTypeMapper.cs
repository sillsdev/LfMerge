// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Core.LanguageForge.Config
{
	// TODO: Is this extra layer of indirection really benefitting us? Maybe MongoRegistrar should
	// take a BidirectionalDictionary instead of an LfFieldTypeMapper.
	public class LfFieldTypeMapper
	{
		public BidirectionalDictionary<string, Type> Map { get; private set; }

		public LfFieldTypeMapper(BidirectionalDictionary<string, Type> map)
		{
			Map = map;
		}

		public LfFieldTypeMapper() : this(new BidirectionalDictionary<string, Type>())
		{
		}

		public Type GetFieldType(string fieldName)
		{
			return Map.GetByFirst(fieldName); // No TryGetValue: if not found, we WANT an exception to be thrown.
		}

		public string GetFieldName(Type fieldType)
		{
			return Map.GetBySecond(fieldType);
		}

		public object CreateFieldByName(string fieldName)
		{
			Type type = GetFieldType(fieldName);
			return Activator.CreateInstance(type);
		}
	}
}

