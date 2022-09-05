// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.Utilities;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Settings;
using Microsoft.DotNet.PlatformAbstractions;
using NUnit.Framework;
using SIL.LCModel;
using SIL.TestUtilities;
using System.Text.RegularExpressions;

namespace LfMerge.Core.Tests.Actions
{
	/// <summary>
	/// These tests test the LfMergeBridge interface. Because LfMergeBridge uses a single method
	/// and dictionary for input and a string for output the compiler won't complain when the
	/// "interface" changes (e.g. when we suddenly get a different string back than before, or
	/// the name for an expected key changes). These tests try to cover different scenarios so
	/// that we get test failures if the interface changes.
	/// </summary>
	[TestFixture]
	[Platform(Exclude = "Win")]
	[Category("IntegrationTests")]
	public class SynchronizeActionBridgeIntegrationTests
	{
		private TestEnvironment _env;
		private LfMergeSettings _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private LanguageForgeProject _lfProject;
		private SynchronizeAction _synchronizeAction;
		private string _workDir;
		private const string TestLangProj = "testlangproj";
		private const string TestLangProjModified = "testlangproj-modified";

		private string CopyModifiedProjectAsTestLangProj(string webWorkDirectory)
		{
			var repoDir = Path.Combine(webWorkDirectory, TestLangProj);
			if (Directory.Exists(repoDir))
				Directory.Delete(repoDir, true);
			TestEnvironment.CopyFwProjectTo(TestLangProjModified, webWorkDirectory);
			Directory.Move(Path.Combine(webWorkDirectory, TestLangProjModified), repoDir);
			return repoDir;
		}

		private static int ModelVersion
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return chorusHelper.ModelVersion;
			}
		}

		[SetUp]
		public void Setup()
		{
			MagicStrings.SetMinimalModelVersion(LcmCache.ModelVersion);
			_env = new TestEnvironment();
			_languageDepotFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name + Path.GetRandomFileName());
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LanguageDepotMock.ProjectFolderPath =
				Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			Directory.CreateDirectory(LanguageDepotMock.ProjectFolderPath);
			_lfProject = LanguageForgeProject.Create(TestLangProj);
			_synchronizeAction = new SynchronizeAction(_env.Settings, _env.Logger);
			_workDir = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(ExecutionEnvironment.DirectoryOfExecutingAssembly);
			LanguageDepotMock.Server = new MercurialServer(LanguageDepotMock.ProjectFolderPath);
		}

		[TearDown]
		public void Teardown()
		{
			// Reset workdir, otherwise NUnit will get confused when it tries to reset the
			// workdir after running the test but the current dir got deleted in the teardown.
			Directory.SetCurrentDirectory(_workDir);

			LanguageForgeProject.DisposeFwProject(_lfProject);

			if (_languageDepotFolder != null)
				_languageDepotFolder.Dispose();
			_env.Dispose();

			if (LanguageDepotMock.Server != null)
			{
				LanguageDepotMock.Server.Stop();
				LanguageDepotMock.Server = null;
			}
		}

		[Test]
		public void MissingFwDataFixer_Throws()
		{
			// Setup
			var tmpFolder = Path.Combine(_languageDepotFolder.Path, "WorkDir");
			Directory.CreateDirectory(tmpFolder);
			Directory.SetCurrentDirectory(tmpFolder);

			// Execute/Verify
			Assert.That(() => _synchronizeAction.Run(_lfProject),
				// This can't happen in real life because we ensure that we have a clone
				// before we call sync. Therefore it is acceptable to get an exception.
				Throws.TypeOf<InvalidOperationException>());
		}

		[Test]
		public void Error_NoHgRepo()
		{
			// Setup
			Directory.CreateDirectory(_lfProject.ProjectDir);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				Does.Contain("Cannot create a repository at this point in LF development."));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_NoCommitsInRepository()
		{
			// Setup
			// Create a empty hg repo
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, LcmCache.ModelVersion);
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			LanguageDepotMock.Server.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				Does.Contain("Cannot do first commit."));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_DifferentBranch()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			const int modelVersion = 7000067;
			MagicStrings.SetMinimalModelVersion(modelVersion);
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, modelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath, modelVersion);
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			LanguageDepotMock.Server.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
			Assert.That(ModelVersion, Is.EqualTo(modelVersion));
		}

		[Test]
		public void Error_NewerBranch()
		{
			// Setup
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, LcmCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			// Simulate a user with a newer FLEx version doing a S/R
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, 7600000);
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "Commit with newer FLEx version");
			LanguageDepotMock.Server.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetMessages(), Does.Contain("Allow data migration for project"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Error_InvalidUtf8InXml()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			LanguageDepotMock.Server.Start();
			var ldDirectory = Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);
			var fwdataPath = Path.Combine(_env.Settings.WebWorkDirectory, TestLangProj, TestLangProj + ".fwdata");
			TestEnvironment.OverwriteBytesInFile(fwdataPath, new byte[] {0xc0, 0xc1}, 25);  // 0xC0 and 0xC1 are always invalid byte values in UTF-8

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			string errors = _env.Logger.GetErrors();
			Assert.That(errors, Does.Contain("System.Xml.XmlException"));
			// Stack trace should also have been logged
			var regex = new Regex(@"^\s*at Chorus\.sync\.Synchronizer\.SyncNow.*SyncOptions options", RegexOptions.Multiline);
			Assert.That(errors, Does.Match(regex));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_WrongXmlEncoding()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			LanguageDepotMock.Server.Start();
			var ldDirectory = Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);
			var fwdataPath = Path.Combine(_env.Settings.WebWorkDirectory, TestLangProj, TestLangProj + ".fwdata");
			TestEnvironment.ChangeFileEncoding(fwdataPath, System.Text.Encoding.UTF8, System.Text.Encoding.UTF32);
			// Note that the XML file will still claim the encoding is UTF-8!

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			string errors = _env.Logger.GetErrors();
			Assert.That(errors, Does.Contain("System.Xml.XmlException: '.', hexadecimal value 0x00, is an invalid character."));
			// Stack trace should also have been logged
			var regex = new Regex(@"^\s*at Chorus\.sync\.Synchronizer\.SyncNow.*SyncOptions options", RegexOptions.Multiline);
			Assert.That(errors, Does.Match(regex));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		public void Success_NewBranchFormat_LfMerge68()
		{
			// Setup
			var ldDirectory = CopyModifiedProjectAsTestLangProj(_lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			TestEnvironment.WriteTextFile(Path.Combine(_env.Settings.WebWorkDirectory, TestLangProj, "FLExProject.ModelVersion"), "{\"modelversion\": 7000068}");
			LanguageDepotMock.Server.Start();
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(MercurialTestHelper.GetRevisionOfTip(ldDirectory)),
				"Our repo doesn't have the changes from LanguageDepot");
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("Received changes from others"));
		}

		public void Success_NewBranchFormat_LfMerge69()
		{
			// Setup
			var ldDirectory = CopyModifiedProjectAsTestLangProj(_lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			TestEnvironment.WriteTextFile(Path.Combine(_env.Settings.WebWorkDirectory, TestLangProj, "FLExProject.ModelVersion"), "{\"modelversion\": 7000069}");
			LanguageDepotMock.Server.Start();
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			// Stack trace should also have been logged
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(MercurialTestHelper.GetRevisionOfTip(ldDirectory)),
				"Our repo doesn't have the changes from LanguageDepot");
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("Received changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_NewBranchFormat_LfMerge70()
		{
			// Setup
			var ldDirectory = CopyModifiedProjectAsTestLangProj(_lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			TestEnvironment.WriteTextFile(Path.Combine(_env.Settings.WebWorkDirectory, TestLangProj, "FLExProject.ModelVersion"), "{\"modelversion\": 7000070}");
			LanguageDepotMock.Server.Start();
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(MercurialTestHelper.GetRevisionOfTip(ldDirectory)),
				"Our repo doesn't have the changes from LanguageDepot");
			// Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("Received changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		[Ignore("Temporarily disabled - needs updated test data")]
		public void Success_NoNewChangesFromOthersAndUs()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			LanguageDepotMock.Server.Start();
			var ldDirectory = Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("No changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		[Ignore("Temporarily disabled - needs updated test data")]
		public void Success_ChangesFromOthersNoChangesFromUs()
		{
			// Setup
			var ldDirectory = CopyModifiedProjectAsTestLangProj(_lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			LanguageDepotMock.Server.Start();
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(MercurialTestHelper.GetRevisionOfTip(ldDirectory)),
				"Our repo doesn't have the changes from LanguageDepot");
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("Received changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		[Ignore("Temporarily disabled - needs updated test data")]
		public void Success_ChangesFromUsNoChangesFromOthers()
		{
			// Setup
			CopyModifiedProjectAsTestLangProj(_env.Settings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			LanguageDepotMock.Server.Start();
			var oldHashOfUs = MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(oldHashOfUs));
			Assert.That(MercurialTestHelper.GetRevisionOfTip(
				Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj)),
				Is.EqualTo(oldHashOfUs), "LanguageDepot doesn't have our changes");
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Does.Contain("No changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}
	}
}

