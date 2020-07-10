// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.DataConverters;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using SIL.TestUtilities;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;

namespace LfMerge.Core.Tests.Lcm
{
	/// <summary>
	/// Fixture setup for all LCM tests. Creates a single FW project and sets up
	/// an UndoableUnitOfWork inside it.
	/// </summary>
	[SetUpFixture]
	public class LcmTestFixture
	{
		public const string testProjectCode = "testlangproj";
		public const int originalNumOfLcmEntries = 63;
		public TestEnvironment env;
		public LfMergeSettings Settings;
		public static LanguageForgeProject lfProj;
		public TemporaryFolder LanguageForgeFolder;

		public static TestEnvironment CreateEnvironment()
		{
			return new TestEnvironment();
		}

		[OneTimeSetUp]
		public void SetUpForLcmTests()
		{
			LanguageForgeFolder = new TemporaryFolder("LcmTestFixture");
			env = new TestEnvironment(
				resetLfProjectsDuringCleanup: false,
				languageForgeServerFolder: LanguageForgeFolder
			);
			Settings = new LfMergeSettingsDouble(LanguageForgeFolder.Path);
			TestEnvironment.CopyFwProjectTo(testProjectCode, Settings.LcmDirectorySettings.DefaultProjectsDirectory);
			lfProj = LanguageForgeProject.Create(testProjectCode);
		}

		[OneTimeTearDown]
		public void TearDownForLcmTests()
		{
			IgnoreException(LanguageForgeProjectAccessor.Reset); // This disposes of lfProj
			IgnoreException(LanguageForgeFolder.Dispose);
			IgnoreException(env.Dispose);
		}

		private void IgnoreException(System.Action action)
		{
			try
			{
				action();
			}
			catch (Exception)
			{
				// This can happen if the objects already got disposed somewhere else.
				// It doesn't really matter since we're in the process of doing cleanup anyways.
				// So just ignore the exception.
			}
		}

	}

	public class LcmTestBase
	{
		public const string TestProjectCode = LcmTestFixture.testProjectCode;
		public const int OriginalNumOfLcmEntries = LcmTestFixture.originalNumOfLcmEntries;
		public const string ModifiedTestProjectCode = "testlangproj-modified";

		// common to both test projects
		public const string TestEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc"; // lexeme kal: ztestmain
		public const string TestMinorEntryGuidStr = "a6babda6-6830-4ec0-a363-c7fba14268eb"; // lexeme kal: ztestminor
		public const string TestSubEntryGuidStr = "157edd55-886f-4d91-b009-8e6b49991c85"; // lexeme kal: ztestsub

		// in "testlangproj" only
		public const string KenEntryGuidStr = "c5f97698-dade-4ba0-9f91-580ab19ff411"; // lexeme kal: ken

		// in "testlangproj-modified" only
		public const string IraEntryGuidStr = "ba8076a9-6552-46b2-a14a-14c01191453b"; // lexeme kal: Ira

		protected TestEnvironment _env;
		protected MongoConnectionDouble _conn;
		protected EntryCounts _counts;
		protected MongoProjectRecordFactory _recordFactory;
		protected LanguageForgeProject _lfProj;
		protected LcmCache _cache;
		protected FwServiceLocatorCache _servLoc;
		protected int _wsEn;
		protected Dictionary<string, ConvertLcmToMongoOptionList> _listConverters;
		protected UndoableUnitOfWorkHelper _undoHelper;
		protected TransferMongoToLcmAction SutMongoToLcm;
		protected TransferLcmToMongoAction SutLcmToMongo;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment(resetLfProjectsDuringCleanup: false);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			_counts = MainClass.Container.Resolve<EntryCounts>();
			if (_conn == null)
				throw new AssertionException("LCM tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Mongo->Lcm roundtrip tests need a mock MongoProjectRecordFactory in order to work.");

			_lfProj = LcmTestFixture.lfProj;
			_cache = _lfProj.FieldWorksProject.Cache;
			Assert.That(_cache, Is.Not.Null);
			_servLoc = new FwServiceLocatorCache(_cache.ServiceLocator);
			_wsEn = _cache.WritingSystemFactory.GetWsFromStr("en");
			_undoHelper = new UndoableUnitOfWorkHelper(_cache.ActionHandlerAccessor, "undo", "redo");
			_undoHelper.RollBack = true;

			SutMongoToLcm = new TransferMongoToLcmAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory,
				_counts
			);

			SutLcmToMongo = new TransferLcmToMongoAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);

			var convertCustomField = new ConvertLcmToMongoCustomField(_cache, _servLoc, _env.Logger);
			_listConverters = new Dictionary<string, ConvertLcmToMongoOptionList>();
			foreach (KeyValuePair<string, ICmPossibilityList> pair in convertCustomField.GetCustomFieldParentLists())
			{
				string listCode = pair.Key;
				ICmPossibilityList parentList = pair.Value;
				_listConverters[listCode] = ConvertOptionListFromLcm(_lfProj, listCode, parentList);
			}
		}

		public ConvertLcmToMongoOptionList ConvertOptionListFromLcm(ILfProject project, string listCode, ICmPossibilityList lcmOptionList, bool updateMongoList = true)
		{
			LfOptionList lfExistingOptionList = _conn.GetLfOptionListByCode(project, listCode);
			var converter = new ConvertLcmToMongoOptionList(lfExistingOptionList, _wsEn, listCode, _env.Logger, _cache.WritingSystemFactory);
			LfOptionList lfChangedOptionList = converter.PrepareOptionListUpdate(lcmOptionList);
			if (updateMongoList)
				_conn.UpdateRecord(project, lfChangedOptionList, listCode);
			return new ConvertLcmToMongoOptionList(lfChangedOptionList, _wsEn, listCode, _env.Logger, _cache.WritingSystemFactory);
		}

		[TearDown]
		public void Teardown()
		{
			if (_undoHelper != null)
				_undoHelper.Dispose(); // This executes the undo action on the LCM project
			_conn.Reset();
			_env.Dispose();
		}
	}
}

