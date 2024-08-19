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
		public TemporaryFolder LcmDataFolder { get; set; }
		public string Sena3ZipPath { get; set; }
		private Guid? ProjectIdToDelete { get; set; }
		public SRTestEnvironment TestEnv { get; set; }

		public SRTestBase()
		{
		}

		private static string TestName => TestContext.CurrentContext.Test.Name;
		private static string TestNameForPath => string.Concat(TestName.Split(Path.GetInvalidPathChars())); // Easiest way to strip out all invalid chars

		[OneTimeSetUp]
		public async Task FixtureSetup()
		{
			// Ensure we don't delete top-level /tmp/LfMergeSRTests folder and data subfolder if they already exist
			var tempPath = Path.Combine(Path.GetTempPath(), "LfMergeSRTests");
			var rootTempFolder = Directory.Exists(tempPath) ? TemporaryFolder.TrackExisting(tempPath) : new TemporaryFolder(tempPath);
			var testDataPath = Path.Combine(tempPath, "data");
			TestDataFolder = Directory.Exists(testDataPath) ? TemporaryFolder.TrackExisting(testDataPath) : new TemporaryFolder(testDataPath);
			var lcmDataPath = Path.Combine(tempPath, "lcm-common");
			LcmDataFolder = Directory.Exists(lcmDataPath) ? TemporaryFolder.TrackExisting(lcmDataPath) : new TemporaryFolder(lcmDataPath);
			Environment.SetEnvironmentVariable("FW_CommonAppData", LcmDataFolder.Path);

			// But the folder for this specific test suite should be deleted if it already exists
			var derivedClassName = this.GetType().Name;
			TempFolderForClass = new TemporaryFolder(rootTempFolder, derivedClassName);

			// Ensure sena-3.zip is available to all tests as a starting point
			Sena3ZipPath = Path.Combine(TestDataFolder.Path, "sena-3.zip");
			if (!File.Exists(Sena3ZipPath)) {
				await SRTestEnvironment.Login();
				await SRTestEnvironment.DownloadProjectBackup("sena-3", Sena3ZipPath);
			}
		}

		[OneTimeTearDown]
		public void FixtureTeardown()
		{
			Environment.SetEnvironmentVariable("FW_CommonAppData", null);
			var result = TestContext.CurrentContext.Result;
			var nonSuccess = result.FailCount + result.InconclusiveCount + result.WarningCount;
			// Only delete class temp folder if we passed or skipped all tests
			if (nonSuccess == 0) TempFolderForClass.Dispose();
		}

		[SetUp]
		public async Task TestSetup()
		{
			TempFolderForTest = new TemporaryFolder(TempFolderForClass, TestNameForPath);
			TestEnv = new SRTestEnvironment(TempFolderForTest);
			await SRTestEnvironment.Login();
		}

		[TearDown]
		public async Task TestTeardown()
		{
			// Only delete temp folder if test passed, otherwise we'll want to leave it in place for post-test investigation
			if (TestContext.CurrentContext.Result.Outcome == ResultState.Success) {
				TempFolderForTest.Dispose();
				if (ProjectIdToDelete is not null) {
					var projId = ProjectIdToDelete.Value;
					ProjectIdToDelete = null;
					// Also leave LexBox project in place for post-test investigation, even though this might tend to clutter things up a little
					await SRTestEnvironment.DeleteLexBoxProject(projId);
				}
			}
		}

		public string TestFolderForProject(string projectCode) => Path.Join(TempFolderForTest.Path, "webwork", projectCode);
		public string FwDataPathForProject(string projectCode) => Path.Join(TestFolderForProject(projectCode), $"{projectCode}.fwdata");

		public string CloneRepoFromLexbox(string code, string? newCode = null)
		{
			var projUrl = SRTestEnvironment.LexboxUrlForProjectWithAuth(code);
			newCode ??= code;
			var dest = TestFolderForProject(newCode);
			MercurialTestHelper.CloneRepo(projUrl.AbsoluteUri, dest);
			return dest;
		}

		public FwProject CloneFromLexbox(string code, string? newCode = null)
		{
			var dest = CloneRepoFromLexbox(code, newCode);
			var dirInfo = new DirectoryInfo(dest);
			if (!dirInfo.Exists) throw new InvalidOperationException($"Failed to clone {code} from lexbox, cannot create FwProject");
			var dirname = dirInfo.Name;
			var fwdataPath = Path.Join(dest, $"{dirname}.fwdata");
			MercurialTestHelper.ChangeBranch(dest, "tip");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(SRTestEnvironment.NullProgress, false, fwdataPath);
			var settings = new LfMergeSettingsDouble(TempFolderForTest.Path);
			return new FwProject(settings, dirname);
		}

		public async Task<string> CreateNewFlexProject()
		{
			var randomGuid = Guid.NewGuid();
			var testCode = $"sr-{randomGuid}";
			var testPath = TestFolderForProject(testCode);
			MercurialTestHelper.InitializeHgRepo(testPath);
			MercurialTestHelper.CreateFlexRepo(testPath);
			// Now create project in LexBox
			var result = await SRTestEnvironment.CreateLexBoxProject(testCode, randomGuid);
			Assert.That(result.Result, Is.EqualTo(LexboxGraphQLTypes.CreateProjectResult.Created));
			Assert.That(result.Id, Is.EqualTo(randomGuid));
			// TODO: Push that first commit to lexbox so the project is non-empty
			ProjectIdToDelete = result.Id;
			return testCode;
		}

		public async Task<string> CreateNewProjectFromTemplate(string origZipPath)
		{
			var randomGuid = Guid.NewGuid();
			var testCode = $"sr-{randomGuid}";
			var testPath = TestFolderForProject(testCode);
			// Now create project in LexBox
			var result = await SRTestEnvironment.CreateLexBoxProject(testCode, randomGuid);
			Assert.That(result.Result, Is.EqualTo(LexboxGraphQLTypes.CreateProjectResult.Created));
			Assert.That(result.Id, Is.EqualTo(randomGuid));
			await TestEnv.ResetAndUploadZip(testCode, origZipPath);
			ProjectIdToDelete = result.Id;
			return testCode;
		}

		public Task<string> CreateNewProjectFromSena3() => CreateNewProjectFromTemplate(Sena3ZipPath);

		public void CommitAndPush(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			SRTestEnvironment.CommitAndPush(project, code, TempFolderForTest.Path, localCode, commitMsg);
		}
	}
}
