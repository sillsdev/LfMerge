using System;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.MongoConnector;
using NUnit.Framework;
using SIL.LCModel;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class E2ETestBase : SRTestBase
	{
		public MongoConnectionDouble _mongoConnection;
		public MongoProjectRecordFactory _recordFactory;

		[SetUp]
		public void Setup()
		{
			MagicStrings.SetMinimalModelVersion(LcmCache.ModelVersion);
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
