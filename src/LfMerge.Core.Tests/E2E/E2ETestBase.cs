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

		public async Task<LanguageForgeProject> CreateLfProjectFromSena3()
		{
			var projCode = await CreateNewProjectFromSena3();
			var projPath = CloneRepoFromLexbox(projCode);
			MercurialTestHelper.ChangeBranch(projPath, "tip");
			var fwdataPath = Path.Combine(projPath, $"{projCode}.fwdata");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(SRTestEnvironment.NullProgress, false, fwdataPath);

			// Do an initial clone from LexBox to the mock LF
			var lfProject = LanguageForgeProject.Create(projCode, TestEnv.Settings);
			lfProject.IsInitialClone = true;
			var transferLcmToMongo = new TransferLcmToMongoAction(TestEnv.Settings, SRTestEnvironment.NullLogger, _mongoConnection, _recordFactory);
			transferLcmToMongo.Run(lfProject);

			return lfProject;
		}

		public void SendReceiveToLexbox(LanguageForgeProject lfProject)
		{
			// ChorusHelperDouble.GetSyncUri assumes presence of a LanguageDepotMock, but here we want a real LexBox instance so we override it via environment variable
			var saveEnv = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri);
			try {
				var lexboxRepoUrl = SRTestEnvironment.LexboxUrlForProjectWithAuth(lfProject.ProjectCode).AbsoluteUri;
				Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri, lexboxRepoUrl);
				var syncAction = new SynchronizeAction(TestEnv.Settings, TestEnv.Logger);
				syncAction.Run(lfProject);
			} finally {
				Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri, saveEnv);
			}
		}

		public (string, DateTime, DateTime) UpdateFwGloss(FwProject project, Guid entryId, Func<string, string> textConverter)
		{
			var fwEntry = LcmTestHelper.GetEntry(project, entryId);
			Assert.That(fwEntry, Is.Not.Null);
			var origModifiedDate = fwEntry.DateModified;
			var unchangedGloss = LcmTestHelper.UpdateAnalysisText(project, fwEntry.SensesOS[0].Gloss, textConverter);
			return (unchangedGloss, origModifiedDate, fwEntry.DateModified);
		}

		public (string, DateTime, DateTime) UpdateLfGloss(LanguageForgeProject lfProject, Guid entryId, string wsId, Func<string, string> textConverter)
		{
			var lfEntry = _mongoConnection.GetLfLexEntryByGuid(entryId);
			Assert.That(lfEntry, Is.Not.Null);
			var unchangedGloss = lfEntry.Senses[0].Gloss[wsId].Value;
			lfEntry.Senses[0].Gloss["pt"].Value = textConverter(unchangedGloss);
			// Remember that in LfMerge it's AuthorInfo that corresponds to the Lcm modified date
			DateTime origModifiedDate = lfEntry.AuthorInfo.ModifiedDate;
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(lfProject, lfEntry);
			return (unchangedGloss, origModifiedDate, lfEntry.AuthorInfo.ModifiedDate);
		}
	}
}
