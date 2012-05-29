using System;
using System.IO;
using System.Xml;
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

		private static readonly string s_LiftUpdate3 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'>"
			+ "<lexical-unit><form lang='nan'><text>change ENTRY FOUR again to see if Merge works on same record.</text></form></lexical-unit></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		/// <summary>
		/// 1) Create a lift project and repo in the webWork area
		/// 2) create a couple .lift.update files so that the UpdateProcesser will take action
		/// 5) get the sha's for each stage
		/// 5) run ProcessUpdates
		/// CHECK:
		/// make sure the repo was cloned to the MergeWork folder.
		/// The sha's should match.
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
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				//Verify that if there are updates for a project that the project is Cloned into the MergeWork/Projects
				//folder.
				var projAMergeWorkPath = testEnv.LangForgeDirFinder.GetProjMergePath("ProjA");
				Assert.That(Directory.Exists(projAMergeWorkPath), Is.True);
				var mergeRepo = new HgRepository(projAMergeWorkPath, new NullProgress());
				Assert.That(mergeRepo, Is.Not.Null);
				var mergeRepoRevision = mergeRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevision.Number.Hash, Is.EqualTo(currentRevision.Number.Hash));
				var projLiftFileInMergeArea = LiftFileFullPath(projAMergeWorkPath, "ProjA");
				Assert.That(File.Exists(projLiftFileInMergeArea), Is.True);
			}
		}

		/// <summary>
		/// This test has the following setup.
		/// 1) Create the master .Lift file in WebWork
		/// 2) Clone it to the MergeWork location
		/// 3) Modify the MergeWork/Projects/ProjA/ProjA.lift file, then commit it so the .hg file will have changed.
		/// 4) Create a .lift.update file for this project so that LiftUpdateProcessor will take action on this project.
		/// 5) run ProcessUpdates
		/// CHECK
		/// Make sure the repo was not replaced by the one in WebWork (look at the sha). The point is the repo should
		/// only be cloned if it does not exist in the MergeWork folder.
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
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate1,
												  testEnv.LangForgeDirFinder.LiftUpdatesPath);

				//Run LiftUpdaeProcessor
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				var mergeRepoRevisionAfterProcessLiftUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();

				var projAWebRevision = projAWebRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevisionBeforeChange.Number.Hash, Is.EqualTo(projAWebRevision.Number.Hash));
				Assert.That(mergeRepoRevisionAfterChange.Number.Hash,
							Is.EqualTo(mergeRepoRevisionAfterProcessLiftUpdates.Number.Hash));
				Assert.That(mergeRepoRevisionAfterProcessLiftUpdates.Number.Hash,
							Is.Not.EqualTo(projAWebRevision.Number.Hash));

				//Check the contents of the .lift file
				var xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22']", "SLIGHT CHANGE in .LIFT file");
			}
		}

		private static void VerifyEntryInnerText(XmlDocument xmlDoc, string xPath, string innerText)
		{
			var selectedEntries = VerifyEntryExists(xmlDoc, xPath);
			XmlNode entry = selectedEntries[0];
			Assert.AreEqual(innerText, entry.InnerText, "Text for entry 'two' is wrong.");
		}

		private static XmlNodeList VerifyEntryExists(XmlDocument xmlDoc, string xPath)
		{
			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
			Assert.IsNotNull(selectedEntries);
			Assert.AreEqual(1, selectedEntries.Count, String.Format("An entry with the following criteria should exist:{0}", xPath));
			return selectedEntries;
		}

		private static void VerifyEntryDoesNotExist(XmlDocument xmlDoc, string xPath)
		{
			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
			Assert.IsNotNull(selectedEntries);
			Assert.AreEqual(0, selectedEntries.Count,
							String.Format("An entry with the following criteria should not exist:{0}", xPath));
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
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessLiftUpdates();

				// .lift.update files are deleted when they are processed. Make sure this happens so they are not processed again.
				Assert.That(File.Exists(liftUpdateFile1), Is.False);
				Assert.That(File.Exists(liftUpdateFile2), Is.False);

				//No commits should have been done.
				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				Assert.That(mergeRepoRevisionBeforeUpdates.Number.Hash, Is.EqualTo(mergeRepoRevisionAfterUpdates.Number.Hash));

				//We started with one revision so we should still have just one revision since no commits should have
				//been applied yet.
				var allRevisions = projAMergeRepo.GetAllRevisions();
				Assert.That(allRevisions.Count, Is.EqualTo(1));

				//Check the contents of the .lift file
				var xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='one']", "ENTRY ONE ADDS lexical unit");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='four']", "ENTRY FOUR adds a lexical unit");
				VerifyEntryExists(xmlDoc, "//entry[@id='five']");
				VerifyEntryExists(xmlDoc, "//entry[@id='six']");
			}
		}


		/// <summary>
		/// 1) Create the ProjA.lift file in the webWork folder
		/// 2) Clone it to the mergeWork folder
		/// 3) Make a change to the .lift file and do a commit
		/// 3) Create two update files; one for each sha
		///
		/// 4) ProcessUpdates one at a time call ProcessUpdatesForAParticularSha
		///         Process updates first for sha0 then for sha1
		///
		/// CHECK
		/// 5) There should be 4 revisions (sha's).
		///
		/// Do not check the content of the .lift file since those tests should be done in lfSynchonicMergerTests
		/// Note:  what else can be checked.
		/// </summary>
		[Test]
		public void Test_OneProject2Revisions_TwoUpdateFilesForDifferentShas_ApplyUpdate0ThenUpdate1()
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
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha0.Number.Hash);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha1.Number.Hash);

				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				//We cannot know sha after updates since updates could be applied in either order
				//since Sha numbers can be anything but we should be at local revision 3
				Assert.That(mergeRepoRevisionAfterUpdates.Number.Hash, Is.EqualTo(mergeRepoSha1.Number.Hash));

				var allRevisions = projAMergeRepo.GetAllRevisions();
				Assert.That(allRevisions.Count, Is.EqualTo(4));

				//There should only be one head after any application of a set of updates.
				Assert.That(projAMergeRepo.GetHeads().Count, Is.EqualTo(1));

				//Check the contents of the .lift file
				// Here are the steps we would expect to have been followed.
				// Before any updates applied
				// sha0 and sha1 and on sha1
				//
				// Applying updates:
				// apply .lift.update to sha0
				//    switch to sha0: commit does nothing
				//    apply .lift.update
				// apply .lift.update to sha1
				//    switch back to sha1: commit produces new sha2 from sha0 and new head
				//       two heads triggers Synchronizer synch.Sych() to Merge sha2 with sha1
				//          result is sha0, sha1, sha2 and sha3 (where sha3 is the merge of sha1 & sha2)
				//    apply .lift.update to sha1
				//        switch to sha1 and apply the changes in .lift.update
				//
				// results:
				// sha2 should have the changes from the first update.
				// sha3 should have the merge of sha1 & sha2 with other .lift.update applied to it.

				//At this point we should be at sha1 and changes to the .lift file applied to the file but should not be committed yet.
				XmlDocument xmlDoc;
				xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='one']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='two']", "SLIGHT CHANGE in .LIFT file");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='three']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='four']", "ENTRY FOUR adds a lexical unit");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='six']", "");
				VerifyEntryDoesNotExist(xmlDoc, "//entry[@id='five']");

				//Now change to sha2 which was produced after the update to sha0 was committed.
				projAMergeRepo.Update("2");
				xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='one']", "ENTRY ONE ADDS lexical unit");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='two']", "TEST");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='three']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='four']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='five']", "");
				VerifyEntryDoesNotExist(xmlDoc, "//entry[@id='six']");

				//Now check sha3 to see if the merge operation produced the results we would expect.
			}
		}

		/// <summary>
		/// After setup we have sha0 and sha1
		/// Process updates  sha1 then sha0
		///
		/// CHECK
		/// There should be 3 revisions (sha's).
		///
		/// </summary>
		[Test]
		public void Test_OneProject2Revisions_TwoUpdateFilesForDifferentShas_ApplyUpdate1ThenUpdate0()
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
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha1.Number.Hash);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha0.Number.Hash);

				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				//We cannot know sha after updates since updates could be applied in either order
				//since Sha numbers can be anything but we should be at local revision 3
				Assert.That(mergeRepoRevisionAfterUpdates.Number.Hash, Is.EqualTo(mergeRepoSha0.Number.Hash));

				var allRevisions = projAMergeRepo.GetAllRevisions();
				Assert.That(allRevisions.Count, Is.EqualTo(3));

				//There should only be one head after any application of a set of updates.
				Assert.That(projAMergeRepo.GetHeads().Count, Is.EqualTo(1));

				//Check the contents of the .lift file
				//At this point we should be at sha0 and changes to the .lift file should not be committed yet.
				var xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='one']", "ENTRY ONE ADDS lexical unit");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='two']", "TEST");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='three']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='four']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='five']", "");
				VerifyEntryDoesNotExist(xmlDoc, "//entry[@id='six']");

				//Now change to sha2 which was produced after the update to sha1 was committed.
				projAMergeRepo.Update("2");
				xmlDoc = GetResult(testEnv.LangForgeDirFinder.GetProjMergePath("ProjA"), "ProjA");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='one']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='two']", "SLIGHT CHANGE in .LIFT file");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='three']", "");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='four']", "ENTRY FOUR adds a lexical unit");
				VerifyEntryInnerText(xmlDoc, "//entry[@id='six']", "");
				VerifyEntryDoesNotExist(xmlDoc, "//entry[@id='five']");
			}
		}

		[Test]
		public void Test_OneProject2Revisions_TwoUpdateFilesForDifferentShas_ApplyUpdate1ThenUpdate0_ThenAnotherUpdate1()
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
				var lfProcessor = new LiftUpdateProcessor(testEnv.LanguageForgeFolder);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha1.Number.Hash);
				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha0.Number.Hash);

				//Create another .lift.update file  for the second sha
				liftUpdateFileName = GetLiftUpdateFileName("ProjA", mergeRepoSha1, "extraB");
				LfSynchronicMergerTests.WriteFile(liftUpdateFileName, s_LiftUpdate3, testEnv.LangForgeDirFinder.LiftUpdatesPath);

				lfProcessor.ProcessUpdatesForAParticularSha("ProjA", projAMergeRepo, mergeRepoSha1.Number.Hash);

				var mergeRepoRevisionAfterUpdates = projAMergeRepo.GetRevisionWorkingSetIsBasedOn();
				//We cannot know sha after updates since updates could be applied in either order
				//since Sha numbers can be anything but we should be at local revision 3
				Assert.That(mergeRepoRevisionAfterUpdates.Number.Hash, Is.EqualTo(mergeRepoSha1.Number.Hash));

				var allRevisions = projAMergeRepo.GetAllRevisions();
				Assert.That(allRevisions.Count, Is.EqualTo(5));

				//There should only be one head after any application of a set of updates.
				Assert.That(projAMergeRepo.GetHeads().Count, Is.EqualTo(1));
			}
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

		private static XmlDocument GetResult(string directory, string projectName)
		{
			XmlDocument doc = new XmlDocument();
			string outputPath = Path.Combine(directory, projectName + ".lift");
			doc.Load(outputPath);
			Console.WriteLine(File.ReadAllText(outputPath));
			return doc;
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
