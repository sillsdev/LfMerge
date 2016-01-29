// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Chorus.VcsDrivers.Mercurial;
using LfMerge.Actions;
using NUnit.Framework;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ReceiveActionTests
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
		public void DoRun_ProjectDoesntExistSetsStateOnHold()
		{
			// Setup
			var nonExistingProject = Path.GetRandomFileName();

			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(false);

			var lfProj = LanguageForgeProject.Create(_env.Settings, nonExistingProject);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute/Verify
			Assert.That(() => sut.Run(lfProj), Throws.InstanceOf<RepositoryAuthorizationException>());

			// Verify
			Assert.That(lfProj.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void DoRun_DirDoesntExistClonesProject()
		{
			// Setup
			var projCode = TestContext.CurrentContext.Test.Name;
			var lfProj = LanguageForgeProject.Create(_env.Settings, projCode);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute
			sut.Run(lfProj);

			// Verify
			var projDir = Path.Combine(_env.Settings.WebWorkDirectory, projCode.ToLowerInvariant());
			Assert.That(Directory.Exists(projDir), Is.True,
				"Didn't create webwork directory");
			Assert.That(Directory.Exists(Path.Combine(projDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void DoRun_HgDoesntExistClonesProject()
		{
			// Setup
			var projCode = TestContext.CurrentContext.Test.Name;
			var projDir = Path.Combine(_env.Settings.WebWorkDirectory, projCode.ToLowerInvariant());
			Directory.CreateDirectory(projDir);
			var lfProj = LanguageForgeProject.Create(_env.Settings, projCode);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute
			sut.Run(lfProj);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projDir, ".hg")), Is.True,
				"Didn't clone project");
		}

	}
}

