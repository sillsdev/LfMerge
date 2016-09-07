// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using Palaso.IO;
using Palaso.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;

namespace LfMerge.Core.Tests.Fdo
{
	/// <summary>
	/// Fixture setup for all FDO tests. Creates a single FW project and sets up
	/// an UndoableUnitOfWork inside it.
	/// </summary>
	[SetUpFixture]
	public class FdoTestFixture
	{
		public const string testProjectCode = "testlangproj";
		public const int originalNumOfFdoEntries = 63;
		public TestEnvironment env;
		public LfMergeSettings Settings;
		public static LanguageForgeProject lfProj;
		public TemporaryFolder LanguageForgeFolder;

		public static TestEnvironment CreateEnvironment()
		{
			return new TestEnvironment();
		}

		[SetUp]
		public void SetUpForFdoTests()
		{
			LanguageForgeFolder = new TemporaryFolder("FdoTestFixture");
			env = new TestEnvironment(
				resetLfProjectsDuringCleanup: false,
				languageForgeServerFolder: LanguageForgeFolder
			);
			Settings = new LfMergeSettingsDouble(LanguageForgeFolder.Path);
			TestEnvironment.CopyFwProjectTo(testProjectCode, Settings.DefaultProjectsDirectory);
			lfProj = LanguageForgeProject.Create(Settings, testProjectCode);
		}

		[TearDown]
		public void TearDownForFdoTests()
		{
			try
			{
				LanguageForgeProjectAccessor.Reset(); // This disposes of lfProj
				LanguageForgeFolder.Dispose();
				env.Dispose();
			}
			catch (Exception)
			{
				// This can happen if the objects already got disposed somewhere else.
				// It doesn't really matter since we're in the process of doing cleanup anyways.
				// So just ignore the exception.
			}
		}

	}

	public class FdoTestBase
	{
		public const string TestProjectCode = FdoTestFixture.testProjectCode;
		public const int OriginalNumOfFdoEntries = FdoTestFixture.originalNumOfFdoEntries;
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
		protected FdoCache _cache;
		protected ConvertWritingSystems _wsConverter;
		protected int _wsEn;
		protected Dictionary<string, ConvertFdoToMongoOptionList> _listConverters;
		protected UndoableUnitOfWorkHelper _undoHelper;
		protected TransferMongoToFdoAction sutMongoToFdo;
		protected TransferFdoToMongoAction sutFdoToMongo;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment(resetLfProjectsDuringCleanup: false);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			_counts = MainClass.Container.Resolve<EntryCounts>();
			if (_conn == null)
				throw new AssertionException("FDO tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Mongo->Fdo roundtrip tests need a mock MongoProjectRecordFactory in order to work.");

			_lfProj = FdoTestFixture.lfProj;
			_cache = _lfProj.FieldWorksProject.Cache;
			_wsConverter = new ConvertWritingSystems(_cache);
			_wsEn = _cache.WritingSystemFactory.GetWsFromStr("en");
			_undoHelper = new UndoableUnitOfWorkHelper(_cache.ActionHandlerAccessor, "undo", "redo");
			_undoHelper.RollBack = true;

			sutMongoToFdo = new TransferMongoToFdoAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory,
				_counts
			);

			sutFdoToMongo = new TransferFdoToMongoAction(
				_env.Settings,
				_env.Logger,
				_conn
			);

			var convertCustomField = new ConvertFdoToMongoCustomField(_cache, _env.Logger);
			_listConverters = new Dictionary<string, ConvertFdoToMongoOptionList>();
			foreach (KeyValuePair<string, ICmPossibilityList> pair in convertCustomField.GetCustomFieldParentLists())
			{
				string listCode = pair.Key;
				ICmPossibilityList parentList = pair.Value;
				_listConverters[listCode] = ConvertOptionListFromFdo(_lfProj, listCode, parentList);
			}
		}

		public ConvertFdoToMongoOptionList ConvertOptionListFromFdo(ILfProject project, string listCode, ICmPossibilityList fdoOptionList, bool updateMongoList = true)
		{
			LfOptionList lfExistingOptionList = _conn.GetLfOptionListByCode(project, listCode);
			var converter = new ConvertFdoToMongoOptionList(lfExistingOptionList, _wsEn, listCode, _env.Logger, _wsConverter);
			LfOptionList lfChangedOptionList = converter.PrepareOptionListUpdate(fdoOptionList);
			if (updateMongoList)
				_conn.UpdateRecord(project, lfChangedOptionList, listCode);
			return new ConvertFdoToMongoOptionList(lfChangedOptionList, _wsEn, listCode, _env.Logger, _wsConverter);
		}

		[TearDown]
		public void Teardown()
		{
			if (_undoHelper != null)
				_undoHelper.Dispose(); // This executes the undo action on the FDO project
			_conn.Reset();
			_env.Dispose();
		}
	}
}

