using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.DataConverters;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using LfMergeBridge.LfMergeModel;
using NUnit.Framework;
using SIL.LCModel;
using SIL.Progress;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class E2ETestBase : SRTestBase
	{
		public LfMergeSettings _lDSettings;
		public TemporaryFolder _languageDepotFolder;
		public LanguageForgeProject _lfProject;
		public SynchronizeAction _synchronizeAction;
		public MongoConnectionDouble _mongoConnection;
		public MongoProjectRecordFactory _recordFactory;
		public string _workDir;
		public const string TestLangProj = "testlangproj";
		public const string TestLangProjModified = "testlangproj-modified";

		public void LcmSendReceive(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			// LfMergeBridge.LfMergeBridge.Execute??
		}


		[SetUp]
		public void Setup()
		{
			MagicStrings.SetMinimalModelVersion(LcmCache.ModelVersion);

			// SyncActionTests used to create mock LD server -- not needed since we use real (local) LexBox here

			// _languageDepotFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name + Path.GetRandomFileName());
			// _lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			// Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			// LanguageDepotMock.ProjectFolderPath =
			// 	Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			// Directory.CreateDirectory(LanguageDepotMock.ProjectFolderPath);
			// LanguageDepotMock.Server = new MercurialServer(LanguageDepotMock.ProjectFolderPath);

			// SyncActionTests used to create local LF project from the embedded test data -- here we will let each test do that

			// _lfProject = LanguageForgeProject.Create(TestLangProj);

			// TODO: get far enough to need to test this
			_synchronizeAction = new SynchronizeAction(TestEnv.Settings, TestEnv.Logger);

			// SyncActionTests used to set the current working directory so that Mercurial could be found; no longer needed now

			// _workDir = Directory.GetCurrentDirectory();
			// Directory.SetCurrentDirectory(ExecutionEnvironment.DirectoryOfExecutingAssembly);

			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_mongoConnection == null)
				throw new AssertionException("E2E tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("E2E tests need a mock MongoProjectRecordFactory in order to work.");

		}


	}
}
