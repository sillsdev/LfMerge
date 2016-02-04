// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using NUnit.Framework;
using LfMerge;
using LfMerge.Actions;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using LfMerge.LanguageForge.Model;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	public class UpdateMongoFromFdoActionTests
	{
		public const string testProjectCode = "TestLangProj";
		private TestEnvironment _env;
		private MongoConnectionDouble _conn;
		private UpdateMongoDbFromFdo sut;

		[SetUp]
		public void Setup()
		{
			//_env = new TestEnvironment();
			_env = new TestEnvironment(testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("Fdo->Mongo tests need a mock MongoConnection in order to work.");
			// TODO: If creating our own Mocks would be better than getting them from Autofac, do that instead.

			sut = new UpdateMongoDbFromFdo(_env.Settings, _env.Logger, _conn);
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void Action_Should_UpdateLexemes()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			// Exercise
			sut.Run(lfProj);

			// Verify
			string[] searchOrder = new string[] { "en", "fr" };
			string expectedLexeme = "zitʰɛstmen";
			string expectedGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";

			IEnumerable<object> receivedData = _conn.StoredDataByGuid[MagicStrings.LfCollectionNameForLexicon].Values;
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			LfLexEntry entry = receivedData.OfType<LfLexEntry>().FirstOrDefault(e => e.Guid.ToString() == expectedGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.Lexeme.BestString(searchOrder), Is.EqualTo(expectedLexeme));
		}
	}
}

