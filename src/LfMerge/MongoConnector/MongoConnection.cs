// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Autofac;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;

namespace LfMerge
{
	public class MongoConnection : IMongoConnection
	{
		private string connectionString;
		private Lazy<IMongoClient> client;

		// TODO: Get these from config instead of hard-coding
		public static string MainDatabaseName = "scriptureforge";
		public static string HostNameAndPort = "localhost:27017";

		public static void Initialize(string hostName = null, string mainDatabaseName = null)
		{
			if (hostName != null) HostNameAndPort = hostName;
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

		public MongoConnection(string hostNameAndPort = null)
		{
			if (hostNameAndPort == null) hostNameAndPort = HostNameAndPort;
			connectionString = String.Format("mongodb://{0}", hostNameAndPort);
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

		public UpdateDefinition<TDocument> BuildUpdate<TDocument>(TDocument doc) {
			var builder = Builders<TDocument>.Update;
			var updates = new List<UpdateDefinition<TDocument>>();
			foreach (PropertyInfo prop in typeof(TDocument).GetProperties())
			{
				if (prop.PropertyType == typeof(MongoDB.Bson.ObjectId))
					continue; // Mongo doesn't allow changing Mongo IDs
				if (prop.GetValue(doc) == null)
					continue; // Don't persist empty or null values
				switch (prop.PropertyType.Name)
				{
				case "BsonDocument":
					updates.Add(builder.Set(prop.Name, (BsonDocument)prop.GetValue(doc)));
					break;
				case "Guid":
					if ((Guid)prop.GetValue(doc) == Guid.Empty)
						continue; // Don't persist empty or null values
					updates.Add(builder.Set(prop.Name, ((Guid)prop.GetValue(doc)).ToString()));
					break;
				case "LfAuthorInfo":
					updates.Add(builder.Set(prop.Name, (LfAuthorInfo)prop.GetValue(doc)));
					break;
				case "LfMultiText":
					if (((LfMultiText)prop.GetValue(doc)).IsEmpty)
						continue; // Don't persist empty or null values
					updates.Add(builder.Set(prop.Name, (LfMultiText)prop.GetValue(doc)));
					break;
				case "LfStringArrayField":
					if (((LfStringArrayField)prop.GetValue(doc)).IsEmpty)
						continue; // Don't persist empty or null values
					updates.Add(builder.Set(prop.Name, (LfStringArrayField)prop.GetValue(doc)));
					break;
				case "LfStringField":
					if (((LfStringField)prop.GetValue(doc)).IsEmpty)
						continue; // Don't persist empty or null values
					updates.Add(builder.Set(prop.Name, (LfStringField)prop.GetValue(doc)));
					break;
				case "List`1":
					//Console.WriteLine("List of what? Apparently, {0}", prop.PropertyType.GenericTypeArguments[0].Name);
					switch (prop.PropertyType.GenericTypeArguments[0].Name)
					{
					case "LfSense":
						updates.Add(builder.Set(prop.Name, (List<LfSense>)prop.GetValue(doc)));
						break;
					case "LfExample":
						updates.Add(builder.Set(prop.Name, (List<LfExample>)prop.GetValue(doc)));
						break;
					case "LfPicture":
						updates.Add(builder.Set(prop.Name, (List<LfPicture>)prop.GetValue(doc)));
						break;
					}
					break;
				default:
					//Console.WriteLine("Unknown type {0}", prop.PropertyType.Name);
					updates.Add(builder.Set(prop.Name, prop.GetValue(doc)));
					break;
				}
			}
			return builder.Combine(updates);
		}
	}
}

