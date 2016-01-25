// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using System;

namespace LfMerge.Tests.Actions
{
	public class UpdateFdoFromMongoActionTests
	{
		public const string testProjectCode = "TestLangProj";
		private TestEnvironment _env;
		private MongoConnectionDouble _conn;
		private MongoProjectRecordFactory _recordFactory;
		private UpdateFdoFromMongoDbAction sut;

		[SetUp]
		public void Setup()
		{
			//_env = new TestEnvironment();
			_env = new TestEnvironment(testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("Fdo->Mongo tests need a mock MongoConnection in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Fdo->Mongo tests need a mock MongoProjectRecordFactory in order to work.");
			// TODO: If creating our own Mocks would be better than getting them from Autofac, do that instead.

			sut = new UpdateFdoFromMongoDbAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);
		}

		[TearDown]
		public void TearDown()
		{
			// If the current test failed, leave its environment around for manual inspection
			if (TestContext.CurrentContext.Result.Status != TestStatus.Failed)
			{
				_env.Dispose();
			}
		}

		[Test]
		public void Action_Should_UpdateDefinitions()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			_conn.AddToMockData(SampleData.jsonTestData.Replace(
				"This is the English definition",
				"New definition for this unit test"
			));

			// Exercise
			sut.Run(lfProj);

			// Verify
			FdoCache cache = lfProj.FieldWorksProject.Cache;
			// TODO: Get expected data programmatically from SampleData instead of hardcoding it here
			string expectedGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
			string expectedShortName = "ztestmain";
			string expectedDefinition = "New definition for this unit test";
			Guid expectedGuid = Guid.Parse(expectedGuidStr);

			var entry = cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.IsNotNull(entry);
			Assert.That(entry.Guid, Is.EqualTo(expectedGuid));
			Assert.That(entry.ShortName, Is.EqualTo(expectedShortName));
			Assert.That(entry.SensesOS[0].DefinitionOrGloss.BestAnalysisAlternative.Text, Is.EqualTo(expectedDefinition));
		}
	}
}

