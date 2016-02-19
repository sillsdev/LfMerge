// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	public class RoundTripBase
	{
		protected TestEnvironment _env;
		protected MongoConnectionDouble _conn;
		protected MongoProjectRecordFactory _recordFactory;
		protected UpdateFdoFromMongoDbAction sutMongoToFdo;
		protected UpdateMongoDbFromFdo sutFdoToMongo;

		public const string testProjectCode = "TestLangProj";
		public const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";

		[SetUp]
		public void Setup()
		{
			//_env = new TestEnvironment();
			_env = new TestEnvironment(testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("Mongo->Fdo roundtrip tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Mongo->Fdo roundtrip tests need a mock MongoProjectRecordFactory in order to work.");
			// TODO: If creating our own Mocks would be better than getting them from Autofac, do that instead.

			sutMongoToFdo = new UpdateFdoFromMongoDbAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);

			sutFdoToMongo = new UpdateMongoDbFromFdo(
				_env.Settings,
				_env.Logger,
				_conn
			);
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

	}
}
