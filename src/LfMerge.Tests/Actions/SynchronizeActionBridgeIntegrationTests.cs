// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using LfMerge.Actions;
using LfMerge.Settings;
using NUnit.Framework;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests.Actions
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
		private LfMergeSettingsIni _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private LanguageForgeProject _lfProject;
		private SynchronizeAction _synchronizeAction;
		private const string TestLangProj = "testlangproj";
		private const string TestLangProjModified = "testlangproj-modified";

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
		}

		[TearDown]
		public void Teardown()
		{
			if (_languageDepotFolder != null)
				_languageDepotFolder.Dispose();
			_env.Dispose();
		}

		[Test]
		public void MissingFwDataFixer_Throws()
		{
			// Setup
			var workDir = Directory.GetCurrentDirectory();
			try
			{
				var tmpFolder = Path.Combine(_languageDepotFolder.Path, "WorkDir");
				Directory.CreateDirectory(tmpFolder);
				Directory.SetCurrentDirectory(tmpFolder);

				// Execute/Verify
				Assert.That(() => _synchronizeAction.Run(_lfProject),
					// This can't happen in real life because we ensure that we have a clone
					// before we call sync. Therefore it is acceptable to get an exception.
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				// Reset workdir, otherwise NUnit will get confused when it tries to reset the
				// workdir after running the test but the current dir got deleted in the teardown.
				Directory.SetCurrentDirectory(workDir);
			}
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

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				Is.StringContaining("pulled a higher higher model"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_NoNewChanges()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj, _lDSettings.WebWorkDirectory);
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Is.StringContaining("No changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		[Ignore("LfMergeBridge currently doesn't return the expected value. See comment in SynchronizeAction.DoRun")]
		public void Success_ChangesFromOthers()
		{
			// Setup
			Directory.Delete(Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj), true);
			TestEnvironment.CopyFwProjectTo(TestLangProjModified, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, TestLangProjModified),
				Path.Combine(_lDSettings.WebWorkDirectory, TestLangProj));
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);

			// Execute
			_synchronizeAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Null.Or.Empty);
			Assert.That(_env.Logger.GetMessages(), Is.StringContaining("Received changes from others"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}
	}
}

