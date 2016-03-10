// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using Palaso.IO;
using Palaso.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.IO;

namespace LfMerge.Tests.Fdo
{
	/// <summary>
	/// Fixture setup for all FDO tests. Creates a single FW project and sets up
	/// an UndoableUnitOfWork inside it.
	/// </summary>
	[SetUpFixture]
	public class FdoTestFixture
	{
		public const string testProjectCode = "TestLangProj";
		public const int originalNumOfFdoEntries = 63;
		public TestEnvironment env;
		public LfMergeSettingsIni Settings;
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
			CopyFwProjectTo(testProjectCode, Settings.DefaultProjectsDirectory);
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

		// TODO: Consider whether these two utility functions belong in a different class
		public static void CopyFwProjectTo(string projectCode, string destDir)
		{
			string dataDir = Path.Combine(FindGitRepoRoot(), "data");
			DirectoryUtilities.CopyDirectory(Path.Combine(dataDir, projectCode), destDir);
			Console.WriteLine("Copied {0} to {1}", projectCode, destDir);
		}

		public static string FindGitRepoRoot(string startDir = null)
		{
			if (String.IsNullOrEmpty(startDir))
				startDir = Directory.GetCurrentDirectory();
			while (!Directory.Exists(Path.Combine(startDir, ".git")))
			{
				var di = new DirectoryInfo(startDir);
				if (di.Parent == null) // We've reached the root directory
				{
					// Last-ditch effort: assume we're in output/Debug, even though we never found .git
					return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
				}
				startDir = Path.Combine(startDir, "..");
			}
			return Path.GetFullPath(startDir);
		}
	}

	public class FdoTestBase
	{
		protected TestEnvironment _env;
		protected MongoConnectionDouble _conn;
		protected MongoProjectRecordFactory _recordFactory;
		protected LanguageForgeProject _lfProj;
		protected FdoCache _cache;
		protected UndoableUnitOfWorkHelper _undoHelper;
		protected TransferMongoToFdoAction sutMongoToFdo;
		protected TransferFdoToMongoAction sutFdoToMongo;

		public const string testProjectCode = FdoTestFixture.testProjectCode;
		public const int originalNumOfFdoEntries = FdoTestFixture.originalNumOfFdoEntries;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment(resetLfProjectsDuringCleanup: false);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("FDO tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Mongo->Fdo roundtrip tests need a mock MongoProjectRecordFactory in order to work.");

			_lfProj = FdoTestFixture.lfProj;
			_cache = _lfProj.FieldWorksProject.Cache;
			_undoHelper = new UndoableUnitOfWorkHelper(_cache.ActionHandlerAccessor, "undo", "redo");
			_undoHelper.RollBack = true;

			sutMongoToFdo = new TransferMongoToFdoAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);

			sutFdoToMongo = new TransferFdoToMongoAction(
				_env.Settings,
				_env.Logger,
				_conn
			);
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

