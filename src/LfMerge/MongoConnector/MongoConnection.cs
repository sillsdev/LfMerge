// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Driver;
// using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

using LfMerge.LanguageForge.Config;

namespace LfMerge
{
	public class MongoConnection
	{
		private string connectionString;

		// TODO: Do we need a singleton? My gut says no, and that we should remove this. 2015-10 RM
		// OTOH, it's useful to say MongoConnection.Default.GetConnection() everywhere... 2015-10 RM
		public static MongoConnection Default { get; private set; }

		// TODO: Get this from config instead of hard-coding
		public static string MainDatabaseName = "scriptureforge";

		public static void Initialize(string hostName = "localhost") {
			// TODO: This isn't currently thread-safe. Should wrap the whole thing in a lock, just in case we want to use threads later on.
			if (Default != null)
				return;

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
			// TODO: Should we register them manually here instead? Think about it.
			Default = new MongoConnection(hostName);
		}

		public MongoConnection(string hostName = "localhost")
		{
			connectionString = String.Format("mongodb://{0}", hostName);
		}

		public MongoClient GetConnection()
		{
			return new MongoClient(connectionString);
		}

		public IMongoDatabase GetDatabase(string databaseName) {
			return GetConnection().GetDatabase(databaseName);
		}

		public IMongoDatabase GetProjectDatabase(ILfProject project) {
			return GetDatabase(project.MongoDatabaseName);
		}

		public IMongoDatabase GetMainDatabase() {
			return GetDatabase(MainDatabaseName);
		}
	}
}

