// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Autofac;
using MongoDB.Driver;
// using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

using LfMerge.LanguageForge.Config;

namespace LfMerge
{
	public class MongoConnection : IMongoConnection
	{
		private string connectionString;
		private Lazy<IMongoClient> client;

		// TODO: Get these from config instead of hard-coding
		public static string MainDatabaseName = "scriptureforge";
		public static string HostName = "localhost";

		public static void Initialize(string hostName = null, string mainDatabaseName = null)
		{
			if (hostName != null) HostName = hostName;
			if (mainDatabaseName != null) MainDatabaseName = mainDatabaseName;

			// Serialize Boolean values permissively
			BsonSerializer.RegisterSerializationProvider(new BooleanSerializationProvider());

			// Use CamelCaseName conversions between Mongo and our mapping classes
			var pack = new ConventionPack();
			pack.Add(new CamelCaseElementNameConvention());
			ConventionRegistry.Register(
				"My Custom Conventions",
				pack,
				t => t.FullName.StartsWith("LfMerge."));

			// Register class mappings before opening first connection
			var registrar = new MongoRegistrarForLfConfig();
			registrar.RegisterClassMappings();
		}

		public MongoConnection(string hostName = null)
		{
			if (hostName == null) hostName = HostName;
			connectionString = String.Format("mongodb://{0}", hostName);
			client = new Lazy<IMongoClient>(GetNewConnection);
		}

		public MongoClient GetNewConnection()
		{
			return new MongoClient(connectionString);
		}

		public IMongoDatabase GetDatabase(string databaseName) {
			return client.Value.GetDatabase(databaseName);
		}

		public IMongoDatabase GetProjectDatabase(ILfProject project) {
			return GetDatabase(project.MongoDatabaseName);
		}

		public IMongoDatabase GetMainDatabase() {
			return GetDatabase(MainDatabaseName);
		}
	}
}

