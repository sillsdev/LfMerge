// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.Settings;
using NUnit.Framework;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;

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

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_languageDepotFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name);
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			SynchronizeActionTests.LDProjectFolderPath =
				Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			Directory.CreateDirectory(SynchronizeActionTests.LDProjectFolderPath);
			_lfProject = LanguageForgeProject.Create(_env.Settings, TestLangProj);
			_synchronizeAction = new SynchronizeAction(_env.Settings, _env.Logger);
			_workDir = Directory.GetCurrentDirectory();
			SynchronizeActionTests.LDServer = new MercurialServer(SynchronizeActionTests.LDProjectFolderPath);
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

			if (SynchronizeActionTests.LDServer != null)
			{
				SynchronizeActionTests.LDServer.Stop();
				SynchronizeActionTests.LDServer = null;
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
				Is.StringContaining("Cannot create a repository at this point in LF development."));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_NoCommitsInRepository()
		{
			// Setup
			// Create a empty hg repo
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			SynchronizeActionTests.LDServer.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				Is.StringContaining("Cannot do first commit."));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_DifferentBranch()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7000067");
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath, "7000067");
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			SynchronizeActionTests.LDServer.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				Is.StringContaining("Cannot commit to current branch"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Error_NewerBranch()
		{
			// Setup
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.CloneRepo(lDProjectFolderPath, _lfProject.ProjectDir);
			// Simulate a user with a newer FLEx version doing a S/R
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7100000");
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "Commit with newer FLEx version");
			SynchronizeActionTests.LDServer.Start();

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("pulled a higher higher model"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_NoNewChangesFromOthersAndUs()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			SynchronizeActionTests.LDServer.Start();
			var ldDirectory = Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj);
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Is.StringContaining("No changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_ChangesFromOthersNoChangesFromUs()
		{
			// Setup
			var ldDirectory = CopyModifiedProjectAsTestLangProj(_lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			SynchronizeActionTests.LDServer.Start();
			var oldHashOfLd = MercurialTestHelper.GetRevisionOfTip(ldDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(MercurialTestHelper.GetRevisionOfWorkingSet(_lfProject.ProjectDir),
				Is.EqualTo(MercurialTestHelper.GetRevisionOfTip(ldDirectory)),
				"Our repo doesn't have the changes from LanguageDepot");
			Assert.That(MercurialTestHelper.GetRevisionOfTip(ldDirectory), Is.EqualTo(oldHashOfLd));
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Is.StringContaining("Received changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_ChangesFromUsNoChangesFromOthers()
		{
			// Setup
			CopyModifiedProjectAsTestLangProj(_env.Settings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			SynchronizeActionTests.LDServer.Start();
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
			Assert.That(_env.Logger.GetMessages(), Is.StringContaining("No changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}
	}
}

