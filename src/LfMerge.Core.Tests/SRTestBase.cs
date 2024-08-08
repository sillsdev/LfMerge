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
		private string TipRevToRestore { get; set; } = "";
		public SRTestEnvironment TestEnv { get; set; }

		public SRTestBase()
		{
		}

		private string TestName => TestContext.CurrentContext.Test.Name;
		private string TestNameForPath => string.Join("", TestName.Split(Path.GetInvalidPathChars())); // Easiest way to strip out all invalid chars

		[OneTimeSetUp]
		public async Task FixtureSetup()
		{
			// Log in to LexBox as admin
			var lexboxHostname = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname) ?? "localhost";
			var lexboxProtocol = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol) ?? "http";
			var lexboxPort = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriPort) ?? "80";
			var lexboxUsername = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername) ?? "admin";
			var lexboxPassword = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken) ?? "pass";
			TestEnv = new SRTestEnvironment(lexboxHostname, lexboxProtocol, lexboxPort, lexboxUsername, lexboxPassword);
			await TestEnv.Login();

			// Ensure we don't delete top-level /tmp/LfMergeSRTests folder if it already exists
			var tempPath = Path.Combine(Path.GetTempPath(), "LfMergeSRTests");
			var rootTempFolder = Directory.Exists(tempPath) ? TemporaryFolder.TrackExisting(tempPath) : new TemporaryFolder(tempPath);

			// But the folder for this specific test suite should be deleted if it already exists
			var derivedClassName = this.GetType().Name;
			TempFolderForClass = new TemporaryFolder(rootTempFolder, derivedClassName);
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

		[TearDown]
		public async Task TestTeardown()
		{
			await RestoreRemoteProject();
			// Only delete temp folder if test passed, otherwise we'll want to leave it in place for post-test investigation
			if (TestContext.CurrentContext.Result.Outcome == ResultState.Success) TempFolderForTest.Dispose();
		}

		public async Task RestoreRemoteProject()
		{
			var test = TestContext.CurrentContext.Test;
			if (!string.IsNullOrEmpty(TipRevToRestore) && test.Properties.ContainsKey("projectCode")) {
				var code = test.Properties.Get("projectCode") as string;
				await TestEnv.RollbackProjectToRev(code, TipRevToRestore);
			}
		}

		public FwProject CloneFromLexbox(string code, string? newCode = null)
		{
			return LcmTestHelper.CloneFromLexbox(code, TempFolderForTest.Path, newCode);
		}

		public void CommitChanges(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			LcmTestHelper.CommitChanges(project, code, TempFolderForTest.Path, localCode, commitMsg);
		}
	}
}
