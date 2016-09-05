// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.IO;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using NUnit.Framework;
using Palaso.CommandLineProcessing;
using Palaso.Progress;
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
	public class EnsureCloneActionBridgeIntegrationTests
	{
		private class EnsureCloneActionWithoutMongo: EnsureCloneAction
		{
			public EnsureCloneActionWithoutMongo(LfMergeSettings settings, ILogger logger)
				: base(settings, logger)
			{
			}

			protected override void InitialTransferToMongoAfterClone(ILfProject project)
			{
				// We don't want to do this for these tests, but we still want to set the state
				project.State.SRState = ProcessingState.SendReceiveStates.SYNCING;
			}
		}

		private TestEnvironment _env;
		private LfMergeSettings _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private LanguageForgeProject _lfProject;
		private EnsureCloneAction _EnsureCloneAction;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_languageDepotFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name);
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			SynchronizeActionTests.LDProjectFolderPath =
				Path.Combine(_lDSettings.WebWorkDirectory, TestContext.CurrentContext.Test.Name);
			Directory.CreateDirectory(SynchronizeActionTests.LDProjectFolderPath);
			_lfProject = LanguageForgeProject.Create(_env.Settings,
				TestContext.CurrentContext.Test.Name);
			_EnsureCloneAction = new EnsureCloneActionWithoutMongo(_env.Settings, _env.Logger);
		}

		[TearDown]
		public void Teardown()
		{
			if (_languageDepotFolder != null)
				_languageDepotFolder.Dispose();
			_env.Dispose();
		}

		[Test]
		public void Error_NotAFlexProject()
		{
			// Setup
			// Create a non-FLEx hg repo (in this case an empty repo)
			MercurialTestHelper.InitializeHgRepo(SynchronizeActionTests.LDProjectFolderPath);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("clone is not a FLEx project"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_BranchIsMissing()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7000060"); // simulate a too old version
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("no such branch"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_NoCommitsInRepository()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(),
				// LfMergeBridge contains code to deal with a repo without commits, but that code
				// never executes because the check if we have a FLEx repo hits first.
				//.With.Message.ContainsSubstring("new repository with no commits"));
				Is.StringContaining("clone is not a FLEx project"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Error_CloneHasHigherModelVersion()
		{
			// Setup
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7100000");
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "on branch");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("clone has higher model"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void Success_AlreadyOnIt()
		{
			// Setup
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}

		[Test]
		public void Success_Update()
		{
			// Setup
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "on branch");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
			Assert.That(MercurialTestHelper.GetUsernameFromHgrc(_lfProject.ProjectDir),
				Is.EqualTo("Language Forge"));
		}

		/// <summary>
		/// LfMergeBridge contains code to deal with the situation that the directory already
		/// exists (will clone in different directory), however this will never happen with
		/// LfMerge because we will delete the target directory if it already exists before
		/// calling LfMergeBridge.
		/// </summary>
		[Test]
		public void Success_DirectoryAlreadyExists()
		{
			// Setup
			var lDProjectFolderPath = SynchronizeActionTests.LDProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);

			// Simulate an existing different repo in the target location
			var existingTargetDir = _lfProject.ProjectDir;
			MercurialTestHelper.InitializeHgRepo(existingTargetDir);
			File.WriteAllText(Path.Combine(existingTargetDir, "test"), string.Empty);
			MercurialTestHelper.HgCommit(existingTargetDir, "Initial commit");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING));
		}
	}
}

