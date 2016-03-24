// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Chorus.VcsDrivers.Mercurial;
using NUnit.Framework;
using System;
using System.IO;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ProgramTests
	{
		private TestEnvironment _env;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		[ExpectedException("Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException")]
		public void EnsureClone_ProjectDoesntExist_SetsStateOnHold()
		{
			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(false);

			// Setup
			var nonExistingProjectCode = Path.GetRandomFileName().ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(_env.Settings, nonExistingProjectCode);

			// Execute
			MainClass.EnsureClone(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void EnsureClone_DirDoesntExist_ClonesProject()
		{
			// Setup
			var projectCode = TestContext.CurrentContext.Test.Name.ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(_env.Settings, projectCode);

			// Execute
			MainClass.EnsureClone(lfProject);

			// Verify
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, projectCode);
			Assert.That(Directory.Exists(projectDir), Is.True,
				"Didn't create webwork directory: " + projectDir);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_HgDoesntExist_ClonesProject()
		{
			// Setup
			var projectCode = TestContext.CurrentContext.Test.Name.ToLowerInvariant();
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_env.Settings, projectCode);

			// Execute
			MainClass.EnsureClone(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

	}
}
