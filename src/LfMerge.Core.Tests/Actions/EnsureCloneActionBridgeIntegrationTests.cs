// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
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
	public class EnsureCloneActionBridgeIntegrationTests
	{
		private class EnsureCloneActionWithoutMongo: EnsureCloneAction
		{
			public EnsureCloneActionWithoutMongo(LfMergeSettings settings, ILogger logger, MongoProjectRecordFactory factory, IMongoConnection connection)
				: base(settings, logger, factory, connection)
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
		private MongoProjectRecordFactoryDouble _mongoProjectRecordFactory;
		private EnsureCloneAction _EnsureCloneAction;
		private const string TestLangProj = "testlangproj";

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_languageDepotFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name + Path.GetRandomFileName());
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LanguageDepotMock.ProjectFolderPath =
				Path.Combine(_lDSettings.WebWorkDirectory, TestContext.CurrentContext.Test.Name, TestLangProj);
			Directory.CreateDirectory(LanguageDepotMock.ProjectFolderPath);
			_lfProject = LanguageForgeProject.Create(TestLangProj);
			_mongoProjectRecordFactory = MainClass.Container.Resolve<LfMerge.Core.MongoConnector.MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			// Even though the EnsureCloneActionWithoutMongo class has "WithoutMongo" in the name, the EnsureClone class which it inherits from
			// needs an IMongoConnection argument in the constructor, so we have to create a MongoConnectionDouble that we're not going to use here.
			var _mongoConnection = MainClass.Container.Resolve<IMongoConnection>();
			_EnsureCloneAction = new EnsureCloneActionWithoutMongo(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection);
			LanguageDepotMock.Server = new MercurialServer(LanguageDepotMock.ProjectFolderPath);
		}

		[TearDown]
		public void Teardown()
		{
			if (_languageDepotFolder != null)
				_languageDepotFolder.Dispose();
			_env.Dispose();

			if (LanguageDepotMock.Server != null)
			{
				LanguageDepotMock.Server.Stop();
				LanguageDepotMock.Server = null;
			}
		}

		private static string ModelVersion
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return chorusHelper.ModelVersion;
			}
		}

		[Test]
		public void Error_NotAFlexProject()
		{
			// Setup
			// Create a non-FLEx hg repo (in this case an empty repo)
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			File.WriteAllText(Path.Combine(lDProjectFolderPath, "some.file"),
				"just a test file");
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "Initial commit");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("clone is not a FLEx project"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(ModelVersion, Is.Null);
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.False);
		}

		[Test]
		public void Error_NoBranch_UnsupportedBranch()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7000060"); // simulate a too old version
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MagicStrings.SetMinimalModelVersion("7000068");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("no such branch"));
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining(
				"clone model version '7000060' less than minimal supported model version '7000068'."));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(ModelVersion, Is.Null);
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.False);
		}

		[Test]
		public void Error_NoBranch_OlderSupportedBranch()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7000065"); // simulate a too old version
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MagicStrings.SetMinimalModelVersion("7000065");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			Assert.That(ModelVersion, Is.EqualTo("7000065"));
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.True);
		}

		[Test]
		public void Error_NoCommitsInRepository()
		{
			// Setup
			// Create a hg repo that doesn't contain a branch for the current model version
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("new repository with no commits"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(ModelVersion, Is.Null);
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.False);
		}

		[Test]
		public void Error_CloneHasHigherModelVersion()
		{
			// Setup
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, "7100000");
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "on branch");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.StringContaining("clone has higher model"));
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(ModelVersion, Is.Null);
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.False);
		}

		[Test]
		public void Success_AlreadyOnIt()
		{
			// Setup
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONED));
			Assert.That(ModelVersion, Is.EqualTo(FdoCache.ModelVersion));
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.True);
		}

		[Test]
		public void Success_Update()
		{
			// Setup
			var lDProjectFolderPath = LanguageDepotMock.ProjectFolderPath;
			MercurialTestHelper.InitializeHgRepo(lDProjectFolderPath);
			MercurialTestHelper.CreateFlexRepo(lDProjectFolderPath);
			MercurialTestHelper.HgCreateBranch(lDProjectFolderPath, FdoCache.ModelVersion);
			MercurialTestHelper.HgCommit(lDProjectFolderPath, "on branch");

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONED));
			Assert.That(MercurialTestHelper.GetUsernameFromHgrc(_lfProject.ProjectDir),
				Is.EqualTo("Language Forge"));
			Assert.That(ModelVersion, Is.EqualTo(FdoCache.ModelVersion));
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.True);
		}

		[Test]
		public void Success_DirectoryAlreadyExists_ShouldRecreateFwdataFile()
		{
			// Setup
			TestEnvironment.CopyFwProjectTo(TestLangProj,
				Path.Combine(LanguageDepotMock.ProjectFolderPath, ".."));
			LanguageDepotMock.Server.Start();
			TestEnvironment.CopyFwProjectTo(TestLangProj, _env.Settings.WebWorkDirectory);
			File.Delete(_lfProject.FwDataPath);

			// Execute
			_EnsureCloneAction.Run(_lfProject);

			// Verify
			Assert.That(_env.Logger.GetErrors(), Is.Empty);
			Assert.That(_lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONED));
			Assert.That(ModelVersion, Is.EqualTo(FdoCache.ModelVersion));
			Assert.That(Directory.Exists(_lfProject.ProjectDir), Is.True);
		}
	}
}

