using System;
using System.IO;
using System.Threading.Tasks;
using LfMerge.Core.FieldWorks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests
{
	/// <summary>
	/// Test base class for end-to-end testing, i.e. Send/Receive with a real LexBox instance
	/// </summary>
	public class SRTestBase
	{
		public LfMerge.Core.Logging.ILogger Logger => MainClass.Logger;
		public TemporaryFolder TempFolderForClass { get; set; }
		public TemporaryFolder TempFolderForTest { get; set; }
		public TemporaryFolder TestDataFolder { get; set; }
		public string Sena3ZipPath { get; set; }
		private string TipRevToRestore { get; set; } = "";
		private Guid? ProjectIdToDelete { get; set; }
		public SRTestEnvironment TestEnv { get; set; }

		public SRTestBase()
		{
		}

		private string TestName => TestContext.CurrentContext.Test.Name;
		private string TestNameForPath => string.Join("", TestName.Split(Path.GetInvalidPathChars())); // Easiest way to strip out all invalid chars

		[OneTimeSetUp]
		public async Task FixtureSetup()
		{
			// Log in to LexBox as admin so we get a login cookie
			TestEnv = new SRTestEnvironment();
			await TestEnv.Login();

			// Ensure we don't delete top-level /tmp/LfMergeSRTests folder and data subfolder if they already exist
			var tempPath = Path.Combine(Path.GetTempPath(), "LfMergeSRTests");
			var rootTempFolder = Directory.Exists(tempPath) ? TemporaryFolder.TrackExisting(tempPath) : new TemporaryFolder(tempPath);
			var testDataPath = Path.Combine(tempPath, "data");
			TestDataFolder = Directory.Exists(testDataPath) ? TemporaryFolder.TrackExisting(testDataPath) : new TemporaryFolder(testDataPath);

			// But the folder for this specific test suite should be deleted if it already exists
			var derivedClassName = this.GetType().Name;
			TempFolderForClass = new TemporaryFolder(rootTempFolder, derivedClassName);

			// Ensure sena-3.zip is available to all tests as a starting point
			Sena3ZipPath = Path.Combine(TestDataFolder.Path, "sena-3.zip");
			if (!File.Exists(Sena3ZipPath)) {
				await TestEnv.DownloadProjectBackup("sena-3", Sena3ZipPath);
			}
		}

		[OneTimeTearDown]
		public void FixtureTeardown()
		{
			var result = TestContext.CurrentContext.Result;
			var nonSuccess = result.FailCount + result.InconclusiveCount + result.WarningCount;
			// Only delete class temp folder if we passed or skipped all tests
			if (nonSuccess == 0) TempFolderForClass.Dispose();
		}

		[SetUp]
		public async Task TestSetup()
		{
			TempFolderForTest = new TemporaryFolder(TempFolderForClass, TestNameForPath);
			await BackupRemoteProject();
		}

		[TearDown]
		public async Task TestTeardown()
		{
			await RestoreRemoteProject();
			// Only delete temp folder if test passed, otherwise we'll want to leave it in place for post-test investigation
			if (TestContext.CurrentContext.Result.Outcome == ResultState.Success) {
				TempFolderForTest.Dispose();
				if (ProjectIdToDelete is not null) {
					var projId = ProjectIdToDelete.Value;
					ProjectIdToDelete = null;
					await TestEnv.DeleteLexBoxProject(projId);
				}
			}
		}

		public async Task BackupRemoteProject()
		{
			var test = TestContext.CurrentContext.Test;
			if (test.Properties.ContainsKey("projectCode")) {
				var code = test.Properties.Get("projectCode") as string;
				TipRevToRestore = await TestEnv.GetTipRev(code);
			} else {
				TipRevToRestore = "";
			}
		}

		public async Task RestoreRemoteProject()
		{
			var test = TestContext.CurrentContext.Test;
			if (!string.IsNullOrEmpty(TipRevToRestore) && test.Properties.ContainsKey("projectCode")) {
				var code = test.Properties.Get("projectCode") as string;
				await TestEnv.RollbackProjectToRev(code, TipRevToRestore);
			}
		}

		public string TestFolderForProject(string projectCode) => Path.Join(TempFolderForTest.Path, "webwork", projectCode);
		public string FwDataPathForProject(string projectCode) => Path.Join(TestFolderForProject(projectCode), $"{projectCode}.fwdata");

		public FwProject CloneFromLexbox(string code, string? newCode = null)
		{
			var projUrl = TestEnv.LexboxUrlForProjectWithAuth(code);
			newCode ??= code;
			var dest = Path.Combine(TempFolderForTest.Path, "webwork", newCode);
			MercurialTestHelper.CloneRepo(projUrl.AbsoluteUri, dest);
			var fwdataPath = Path.Join(dest, $"{newCode}.fwdata");
			MercurialTestHelper.ChangeBranch(dest, "tip");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(SRTestEnvironment.NullProgress, false, fwdataPath);
			var settings = new LfMergeSettingsDouble(TempFolderForTest.Path);
			return new FwProject(settings, newCode);

		}

		public async Task<string> CreateNewProjectFromTemplate(string origZipPath)
		{
			var randomGuid = Guid.NewGuid();
			var testCode = $"sr-{randomGuid}";
			var testPath = TestFolderForProject(testCode);
			MercurialTestHelper.InitializeHgRepo(testPath);
			MercurialTestHelper.CreateFlexRepo(testPath);
			// Now create project in LexBox
			var result = await TestEnv.CreateLexBoxProject(testCode, randomGuid);
			Assert.That(result.Result, Is.EqualTo(LexboxGraphQLTypes.CreateProjectResult.Created));
			Assert.That(result.Id, Is.EqualTo(randomGuid));
			await TestEnv.ResetAndUploadZip(testCode, origZipPath);
			ProjectIdToDelete = result.Id;
			return testCode;
		}

		public Task<string> CreateNewProjectFromSena3() => CreateNewProjectFromTemplate(Sena3ZipPath);

		public void CommitAndPush(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			TestEnv.CommitAndPush(project, code, TempFolderForTest.Path, localCode, commitMsg);
		}
	}
}
