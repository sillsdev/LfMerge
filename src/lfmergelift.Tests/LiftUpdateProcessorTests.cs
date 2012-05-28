using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.Progress.LogBox;
using Palaso.TestUtilities;
using Chorus.VcsDrivers.Mercurial;
using NullProgress=Palaso.Progress.LogBox.NullProgress;


namespace lfmergelift.Tests
{
	[TestFixture]
	public class LiftUpdateProcessorTests
	{
		private static readonly string s_LiftDataSha0 =
		   "<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
		   + "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>TEST</text></form></lexical-unit></entry>"
		   + "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

		private static readonly string s_LiftDataSha1 =
		   "<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
		   + "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>SLIGHT CHANGE in .LIFT file</text></form></lexical-unit></entry>"
		   + "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

		private static readonly string s_LiftUpdate1 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'>"
			+                       "<lexical-unit><form lang='nan'><text>ENTRY ONE ADDS lexical unit</text></form></lexical-unit></entry>"
			+ "<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>";

		private static readonly string s_LiftUpdate2 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'>"
			+               "<lexical-unit><form lang='nan'><text>ENTRY FOUR adds a lexical unit</text></form></lexical-unit></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		/// <summary>
		/// 1) Create a lift project and repo in the webWork area
		/// 2) Make a clone of it in the mergeWork area
		/// 3) Make a change to the .lift file in the mergeWork area and commit it to the repo
		/// 4) create a .lift.update file so that the UpdateProcesser will take action
		/// 5) get the sha's for each stage
		/// 5) run ProcessUpdates
		/// CHECK:
		/// make sure the repo in the mergeWork area was not overwritten by the one in the webWork area.
		/// </summary>
		[Test]
		public void Test_OneProject_TwoUpdateFiles_CloneFromWebWorkFolder()
		{
			using (var testEnv = new LangForgeTestEnvironment())
			{
				var projAWebWorkPath = testEnv.LangForgeDirFinder.CreateWebWorkProjectFolder("ProjA");
				//Make the webWork ProjA.LIFT file
				HgRepository projAWebRepo = GetProjAWebRepo(projAWebWorkPath);

				var currentRevision = projAWebRepo.GetRevisionWorkingSetIsBasedOn();

				//Create a .lift.update file. Make sure is has ProjA and the correct Sha(Hash) in the name.
				var liftUpdateFileName = GetLiftUpdateFileName("ProjA", currentRevision, "extraA");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate1, testEnv.LangForgeDirFinder.LiftUpdatesPath);
				//Create another .lift.update file
				liftUpdateFileName = GetLiftUpdateFileName("ProjA", currentRevision, "extraB");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate2, testEnv.LangForgeDirFinder.LiftUpdatesPath);

				//Run LiftUpdaeProcessor
				//Verify that if there are updates for a project that the project is Cloned into the MergeWork/Projects
				//folder.
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				var projAMergeWorkPath = testEnv.LangForgeDirFinder.GetProjMergePath("ProjA");
				Assert.That(Directory.Exists(projAMergeWorkPath), Is.True);
				var mergeRepo = new HgRepository(projAMergeWorkPath, new NullProgress());
				var mergeRepoRevision = mergeRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevision.Number.Hash, Is.EqualTo(currentRevision.Number.Hash));
				Assert.That(mergeRepo, Is.Not.Null);
				var projLiftFileInMergeArea = LiftFileFullPath(projAMergeWorkPath, "ProjA");
				Assert.That(File.Exists(projLiftFileInMergeArea), Is.True);
			}
		}

		/// <summary>
		/// This test has the following setup.
		/// 1) Create the master .Lift file in WebWork
		/// 2) Clone it to the MergeWork location
		/// 3) Modify the MergerWork/Projects/ProjA/ProjA.lift file, then commit it so the .hg file will have changed.
		/// 4) Create a .lift.update file for this project so that LiftUpdateProcessor will take action on this project.
		/// 5) run ProcessUpdates
		/// CHECK
		/// Make sure the repo was not replaced by the one in WebWork (look at the sha)
		/// </summary>
		[Test]
		public void Test_OneProject_MakeSureMergeWorkCopyIsNotOverWritten()
		{
			using (var testEnv = new LangForgeTestEnvironment())
			{
				var projAWebWorkPath = testEnv.LangForgeDirFinder.CreateWebWorkProjectFolder("ProjA");
				//Make the webWork ProjA.LIFT file
				HgRepository projAWebRepo = GetProjAWebRepo(projAWebWorkPath);

				//Make clone of repo in MergeWorkFolder
				var projAMergeWorkPath = testEnv.LangForgeDirFinder.CreateMergeWorkProjectFolder("ProjA");
				HgRepository projAMergeRepo = GetProjAMergeRepoCloned(projAWebRepo, projAMergeWorkPath);

				var mergeRepoRevisionBeforeChange = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				//overwrite the .lift file in the MergeWork folder with this data: s_LiftDataSha1
				MakeProjASha1(projAMergeWorkPath, projAMergeRepo);
				var mergeRepoRevisionAfterChange = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				//Create a .lift.update file. Make sure is has ProjA and the correct Sha(Hash) in the name.
				var liftUpdateFileName = GetLiftUpdateFileName("ProjA", mergeRepoRevisionAfterChange, "extraA");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate1, testEnv.LangForgeDirFinder.LiftUpdatesPath);

				//Run LiftUpdaeProcessor
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				var mergeRepoRevisionAfterProcessLiftUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				var projAWebRevision = projAWebRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevisionBeforeChange.Number.Hash, Is.EqualTo(projAWebRevision.Number.Hash));
				Assert.That(mergeRepoRevisionAfterChange.Number.Hash, Is.EqualTo(mergeRepoRevisionAfterProcessLiftUpdates.Number.Hash));
				Assert.That(mergeRepoRevisionAfterProcessLiftUpdates.Number.Hash, Is.Not.EqualTo(projAWebRevision.Number.Hash));

			}
		}

		private void MakeProjASha1(string projAMergeWorkPath, HgRepository projAMergeRepo)
		{
			LfSynchronicMergerTests.WriteFile("ProjA.Lift", s_LiftDataSha1, projAMergeWorkPath);
			projAMergeRepo.Commit(true, "change made to ProjA.lift file");
		}

		private HgRepository GetProjAMergeRepoCloned(HgRepository projAWebRepo, string projAMergeWorkPath)
		{
			HgRepository projAMergeRepo;

			var repoSourceAddress = RepositoryAddress.Create("LangForge WebWork Repo Location", projAWebRepo.PathToRepo);
			HgRepository.Clone(repoSourceAddress, projAMergeWorkPath, new NullProgress());

			//Note: why was CloneLocal removed????
			//projAWebRepo.CloneLocal(projAMergeWorkPath);   //This copies the .hg file and the ProjA.LIFT file.
			//projAWebRepo.CloneLocalWithoutUpdate(projAMergeWorkPath);
			projAMergeRepo = new HgRepository(projAMergeWorkPath, new NullProgress());
			//projAMergeRepo.Update();
			Assert.That(projAMergeRepo, Is.Not.Null);
			return projAMergeRepo;
		}

		private HgRepository GetProjAWebRepo(string projAWebWorkPath)
		{
			HgRepository projARepo;
			LfSynchronicMergerTests.WriteFile("ProjA.Lift", s_LiftDataSha0, projAWebWorkPath);
			var _progress = new ConsoleProgress();
			HgRepository.CreateRepositoryInExistingDir(projAWebWorkPath, _progress);
			projARepo = new HgRepository(projAWebWorkPath, new NullProgress());

			//Add the .lift file to the repo
			projARepo.AddAndCheckinFile(LiftFileFullPath(projAWebWorkPath, "ProjA"));
			return projARepo;
		}

		/// <summary>
		/// 1) Create the ProjA.lift file in the webWork folder
		/// 2) Clone it to the mergeWork folder
		/// 3) Create two update files for the current sha
		///
		/// 4) ProcessUpdates
		///
		/// CHECK
		/// 5) .lift.update files are deleted
		/// 6) revision number should not be changed because we only do a commit if .lift.update files exist for multiple sha's
		/// 7) We do not want to check the content of the .lift file since those tests should be done in lfSynchonicMergerTests
		/// </summary>
		[Test]
		public void Test_OneProject_TwoUpdateFiles_VerifyUpdatesWereApplied()
		{
			using (var testEnv = new LangForgeTestEnvironment())
			{
				var projAWebWorkPath = testEnv.LangForgeDirFinder.CreateWebWorkProjectFolder("ProjA");
				//Make the webWork ProjA.LIFT file
				HgRepository projAWebRepo = GetProjAWebRepo(projAWebWorkPath);

				var currentRevision = projAWebRepo.GetRevisionWorkingSetIsBasedOn();

				//Make clone of repo in MergeWorkFolder
				var projAMergeWorkPath = testEnv.LangForgeDirFinder.CreateMergeWorkProjectFolder("ProjA");
				HgRepository projAMergeRepo = GetProjAMergeRepoCloned(projAWebRepo, projAMergeWorkPath);

				var mergeRepoRevisionBeforeUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				//Create a .lift.update file. Make sure is has ProjA and the correct Sha(Hash) in the name.
				var liftUpdateFileName = GetLiftUpdateFileName("ProjA", currentRevision, "extraA");
				var liftUpdateFile1 = LiftUpdateFileFullPath(liftUpdateFileName, testEnv);   //check this is deleted after updates are applied
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate1, testEnv.LangForgeDirFinder.LiftUpdatesPath);
				//Create another .lift.update file
				liftUpdateFileName = GetLiftUpdateFileName("ProjA", currentRevision, "extraB");
				var liftUpdateFile2 = LiftUpdateFileFullPath(liftUpdateFileName, testEnv); //check this is deleted after updates are applied
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate2, testEnv.LangForgeDirFinder.LiftUpdatesPath);

				//Run LiftUpdaeProcessor
				//Verify that if there are updates for a project that the project is Cloned into the MergeWork/Projects
				//folder.
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				// .lift.update files are deleted
				Assert.That(File.Exists(liftUpdateFile1), Is.False);
				Assert.That(File.Exists(liftUpdateFile2), Is.False);

				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevisionBeforeUpdates.Number.Hash, Is.EqualTo(mergeRepoRevisionAfterUpdates.Number.Hash));

				//We started with one revision so we should still have just one revision since no commits should have
				//been applied yet.
				var allRevisions = projAMergeRepo.GetAllRevisions();
				Assert.That(allRevisions.Count, Is.EqualTo(1));
			}
		}


		/// <summary>
		/// 1) Create the ProjA.lift file in the webWork folder
		/// 2) Clone it to the mergeWork folder
		/// 3) Make a change to the .lift file and do a commit
		/// 3) Create two update files; one for each sha
		///
		/// 4) ProcessUpdates
		///
		/// CHECK
		/// 5) There should be two revisions (sha's) which match what was there before.
		///  OR mabye there should be 3 depending on the order the sha's were applied.
		/// 6) revision number should be changed because we do a commit if .lift.update files exist for multiple sha's
		/// 7)
		/// 8) We do not want to check the content of the .lift file since those tests should be done in lfSynchonicMergerTests
		/// </summary>
		[Test]
		public void Test_OneProject2Revisions_TwoUpdateFilesForDifferentShas_VerifyUpdatesWereApplied()
		{
			using (var testEnv = new LangForgeTestEnvironment())
			{
				var projAWebWorkPath = testEnv.LangForgeDirFinder.CreateWebWorkProjectFolder("ProjA");
				//Make the webWork ProjA.LIFT file
				HgRepository projAWebRepo = GetProjAWebRepo(projAWebWorkPath);

				//Make clone of repo in MergeWorkFolder
				var projAMergeWorkPath = testEnv.LangForgeDirFinder.CreateMergeWorkProjectFolder("ProjA");
				HgRepository projAMergeRepo = GetProjAMergeRepoCloned(projAWebRepo, projAMergeWorkPath);

				var mergeRepoSha0 = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				//overwrite the .lift file in the MergeWork folder with this data: s_LiftDataSha1
				MakeProjASha1(projAMergeWorkPath, projAMergeRepo);
				var mergeRepoSha1 = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				//We want to make sure the commit happened.
				Assert.That(mergeRepoSha0.Number.Hash, Is.Not.EqualTo(mergeRepoSha1.Number.Hash));

				//Create a .lift.update file. Make sure is has ProjA and the correct Sha(Hash) in the name.
				var liftUpdateFileName = GetLiftUpdateFileName("ProjA", mergeRepoSha0, "extraA");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate1, testEnv.LangForgeDirFinder.LiftUpdatesPath);
				//Create another .lift.update file  for the second sha
				liftUpdateFileName = GetLiftUpdateFileName("ProjA", mergeRepoSha1, "extraB");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate2, testEnv.LangForgeDirFinder.LiftUpdatesPath);

				//Run LiftUpdaeProcessor
				//Verify that if there are updates for a project that the project is Cloned into the MergeWork/Projects
				//folder.
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				//We cannot know revision number after updates since updates could be applied in either order
				//since Sha numbers can be anything
				//Assert.That(mergeRepoSha0.Number.Hash, Is.EqualTo(mergeRepoRevisionAfterUpdates.Number.Hash));

				//We started with one revision so we should still have just one revision since no commits should have
				//been applied yet.
				var allRevisions = projAMergeRepo.GetAllRevisions();
				//Assert.That(allRevisions.Count, Is.EqualTo(3) || Is.EqualTo(4));
				var rev0 = from rev in allRevisions
						   where (rev.Number.Hash == mergeRepoSha0.Number.Hash)
						   select rev;
				Assert.That(rev0.ElementAt(0), Is.Not.Null);
				Assert.That(rev0.ElementAt(0).Number.Hash, Is.EqualTo(mergeRepoSha0.Number.Hash));
				var rev1 = from rev in allRevisions
						   where (rev.Number.Hash == mergeRepoSha1.Number.Hash)
						   select rev;
				Assert.That(rev1.ElementAt(0), Is.Not.Null);
				Assert.That(rev1.ElementAt(0).Number.Hash, Is.EqualTo(mergeRepoSha1.Number.Hash));


			}
		}

		[Test]
		public void Test_TwoProjects_UpdatesAppliedToOnlyOneProject()
		{

		}

		[Test]
		public void Test_TwoProjects_UpdatesAppliedToBothProjects()
		{

		}

		[Test]
		public void Test_OneProjectWithShaAandShaB_UpdatesRequireShaChangeToHappen()
		{

		}

		private String GetLiftUpdateFileName(String projName, Revision rev, String differentiation)
		{
			return projName + "_" + rev.Number.Hash + "_" + differentiation + SynchronicMerger.ExtensionOfIncrementalFiles;
		}

		public const string ExtensionOfLiftFiles = ".lift";
		private static string LiftFileFullPath(String path, String projName)
		{
			return Path.Combine(path, projName + ExtensionOfLiftFiles);
		}

		private static string LiftUpdateFileFullPath(String filename, LangForgeTestEnvironment testEnv)
		{
			return Path.Combine(testEnv.LangForgeDirFinder.LiftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles);
		}
	}

	class LangForgeTestEnvironment : IDisposable
	{
		private readonly TemporaryFolder _languageForgeServerFolder = new TemporaryFolder("LangForge");
		public String LanguageForgeFolder
		{
			get { return _languageForgeServerFolder.Path; }
		}

		public LfDirectoriesAndFiles LangForgeDirFinder
		{
			get { return _langForgeDirFinder; }
		}

		private readonly LfDirectoriesAndFiles _langForgeDirFinder;

		public LangForgeTestEnvironment()
		{
			_langForgeDirFinder = new LfDirectoriesAndFiles(LanguageForgeFolder);
			CreateAllTestFolders();
		}

		private void CreateAllTestFolders()
		{
			LangForgeDirFinder.CreateWebWorkFolder();
			LangForgeDirFinder.CreateMergeWorkFolder();
			LangForgeDirFinder.CreateMergeWorkProjectsFolder();
			LangForgeDirFinder.CreateLiftUpdatesFolder();
		}

		public void Dispose()
		{
			_languageForgeServerFolder.Dispose();
		}
	}
}
