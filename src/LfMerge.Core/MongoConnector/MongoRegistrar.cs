// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Reflection;
using System.Linq;
using LfMerge.Core.LanguageForge.Config;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace LfMerge.Core.MongoConnector
{
	public class MongoRegistrar
	{
		private LfFieldTypeMapper _mapper;

		public virtual void RegisterClassMappings() {}
		public virtual void ExtraRegistrationSteps(Type type, BsonClassMap cm) {}

		public MongoRegistrar() : this(new LfFieldTypeMapper())
		{
		}

		/// <summary>
		///  Use this constructor to pass in a different mapper, e.g. for unit testing
		/// </summary>
		/// <param name="mapper">Mapper.</param>
		public MongoRegistrar(LfFieldTypeMapper mapper)
		{
			_mapper = mapper;
		}

		public void RegisterClassIgnoreExtraFields(Type type)
		{
			BsonClassMap cm = new BsonClassMap(type);
			cm.AutoMap();
			//cm.SetIgnoreExtraElements(true); // Let's see if this is the default
			BsonClassMap.RegisterClassMap(cm);
			//BsonSerializer.RegisterDiscriminatorConvention(type, new ScalarDiscriminatorConvention("type"));
		}

		public void RegisterClassWithDiscriminator(Type type)
		{
			string name = _mapper.GetFieldName(type);
			BsonClassMap cm = new BsonClassMap(type);
			cm.AutoMap();
			cm.SetDiscriminator(name);
			ExtraRegistrationSteps(type, cm); // Derived classes can implement this if they need to.
			BsonClassMap.RegisterClassMap(cm);
			// Don't need to RegisterDiscriminatorConvention for each subclass, only for the base class.
			//BsonSerializer.RegisterDiscriminatorConvention(type, new ScalarDiscriminatorConvention("type"));
		}

		public void RegisterClassMapsForDerivedClassesOf(Type baseClass)
		{
			// TODO: If derived types need extra registration steps, figure out how to discover that here.
			// Current idea is to have an ExtraRegistrationSteps function that does extra steps based on
			// what the type is, and derived classes like MongoRegistrarForLfConfig can override that function.
			BsonSerializer.RegisterDiscriminatorConvention(baseClass, new ScalarDiscriminatorConvention("type"));
			foreach (Type type in
				Assembly.GetAssembly(baseClass).GetTypes()
				.Where(thisType => thisType.IsClass && !thisType.IsAbstract && thisType.IsSubclassOf(baseClass)))
			{
				RegisterClassWithDiscriminator(type);
			}
		}


	}
}
