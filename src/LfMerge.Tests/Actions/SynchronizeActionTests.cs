// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using LfMerge.Tests;
using LfMerge.Tests.Fdo;
using NUnit.Framework;
using Palaso.Progress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	[TestFixture, Explicit, Category("LongRunning")]
	public class SynchronizeActionTests
	{
		public static string ProjectFolderPath;

		private const string testProjectCode = "testlangproj";
		private const int originalNumOfFdoEntries = 63;
		private const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
		private TestEnvironment env;
		private MongoConnectionDouble mongoConnection;
		private MongoProjectRecordFactory _recordFactory;
		private LanguageForgeProject lfProject;
		private Guid testEntryGuid;
		private TransferFdoToMongoAction transferFdoToMongo;
		private SynchronizeAction sutSynchronize;

		[SetUp]
		public void Setup()
		{
			env = new TestEnvironment();
			lfProject = LanguageForgeProject.Create(env.Settings, testProjectCode);
			FdoTestFixture.CopyFwProjectTo(testProjectCode, env.Settings.WebWorkDirectory);
			ProjectFolderPath = Path.Combine(env.Settings.WebWorkDirectory, testProjectCode);
			testEntryGuid = Guid.Parse(testEntryGuidStr);

			mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (mongoConnection == null)
				throw new AssertionException("Sync tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Sync tests need a mock MongoProjectRecordFactory in order to work.");

			transferFdoToMongo = new TransferFdoToMongoAction(env.Settings, env.Logger, mongoConnection);
			sutSynchronize = new SynchronizeAction(env.Settings, env.Logger);
		}

		[TearDown]
		public void Teardown()
		{
			env.Dispose();
		}

		[Test]
		public void SynchronizeAction_NoCloneNoChangedData_GlossUnchanged()
		{
			// Exercise
			sutSynchronize.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}


		[Test]
		public void SynchronizeAction_MongoChangedData_GlossChanged()
		{
			// Setup
			TransferFdoToMongoAction.InitialClone = true;
			transferFdoToMongo.Run(lfProject);
			IEnumerable<LfLexEntry> originalMongoData = mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == testEntryGuid);
			string changedGloss = lfEntry.Senses[0].Gloss["en"].Value + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = changedGloss;

			// Exercise
			sutSynchronize.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(changedGloss));
		}
	}
}
