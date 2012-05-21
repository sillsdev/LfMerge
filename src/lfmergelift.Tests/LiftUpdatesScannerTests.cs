using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.TestUtilities;


namespace lfmergelift.Tests
{
	[TestFixture]
	public class LiftUpdatesScannerTests
	{
		class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _folder = new TemporaryFolder("MergeWork");
			private const String _updatesFolder = "liftUpdates";


			public string MergeWorkFolder
			{
				get { return _folder.Path; }
			}


			public string LiftUpdatesPath
			{
				get { return System.IO.Path.Combine(MergeWorkFolder, _updatesFolder); }
			}

			public void CreateUpdateFolder()
			{
				Directory.CreateDirectory(LiftUpdatesPath);
			}

			public void Dispose()
			{
				_folder.Dispose();
			}

		}

		private const string _baseLiftFileName = "base.lift";

		private static readonly string s_LiftData1 =
		   "<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
		   + "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>"
		   + "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

		private static readonly string s_LiftUpdate1 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>"
			+ "<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>";

		private static readonly string s_LiftUpdate2 =
			"<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		[Test]
		public void FindLiftUpdateFiles_TwoLiftUpdateFiles_OneExtraFile()
		{
			//This test puts 3 files in the mergerWork/liftUpdates folder.  Two are .lift.updates files and one is not.
			//Verify that only the .lift.updates files are returned from the call to GetPendingUpdateFiles
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				////Create a file that is not a .lift.update file
				LfSynchronicMergerTests.WriteFile(_baseLiftFileName, s_LiftData1, e.LiftUpdatesPath);
				//Create a .lift.update file.
				LfSynchronicMergerTests.WriteFile("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, e.LiftUpdatesPath);
				//Create another .lift.update file
				LfSynchronicMergerTests.WriteFile("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);
				FileInfo[] files = LiftUpdatesScanner.GetPendingUpdateFiles(e.LiftUpdatesPath);
				Assert.That(files.Length, Is.EqualTo(2));
				Assert.That(files[0].Name, Is.EqualTo("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles));
				Assert.That(files[1].Name, Is.EqualTo("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles));

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				Assert.That(updatesScanner.LiftUpdateFiles.Length, Is.EqualTo(2));
				Assert.That(updatesScanner.LiftUpdateFiles[0].Name, Is.EqualTo("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles));
				Assert.That(updatesScanner.LiftUpdateFiles[1].Name, Is.EqualTo("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles));
			}
		}

		[Test]
		public void FindLiftUpdateFiles_AndVerifyCorrect_UpdateInfo()
		{
			//This test gets a number of .lift.update files and verifies that the UpdateInfo structure was correctly built
			//Each .lift.update file will have the following format.
			// projX_sha#_timeStamp.lift.update
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				//Create a .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra1" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, e.LiftUpdatesPath);
				//Create a second .lift.update file.
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra2" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				var liftUpdateFilesInfo = updatesScanner.LiftUpdateFilesInfo;
				Assert.IsNotNull(liftUpdateFilesInfo);
				var updateInfoToCheck = from updateInfo in liftUpdateFilesInfo
											   where updateInfo.Project == "ProjA"
											   select updateInfo;
				Assert.That(updateInfoToCheck.Count(), Is.EqualTo(2));
				VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(0), "ProjA", "sha0123", "ProjA_sha0123_uniqueExtra1", e.LiftUpdatesPath);
				VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(1), "ProjA", "sha0123", "ProjA_sha0123_uniqueExtra2", e.LiftUpdatesPath);

				updateInfoToCheck = from updateInfo in liftUpdateFilesInfo
										where updateInfo.Project == "ProjB"
										select updateInfo;
				Assert.That(updateInfoToCheck.Count(), Is.EqualTo(1));
				VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(0), "ProjB", "sha3456", "ProjB_sha3456_time11", e.LiftUpdatesPath);
			}
		}

		void VerifyUpdateInfoRecord(UpdateInfo updateInfoRecord, String proj, String sha, String filename, String liftUpdatesPath)
		{
			Assert.That(updateInfoRecord.Project, Is.EqualTo(proj));
			Assert.That(updateInfoRecord.Sha, Is.EqualTo(sha));
			Assert.That(updateInfoRecord.UpdateFilePath, Is.EqualTo(Path.Combine(liftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles)));
		}

		[Test]
		public void FindAllProjectNamesThatHaveLiftUpdates()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				//Create a .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate1, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra2459" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjK_sha45874563_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate();
				Assert.IsNotNull(projectNames);
				Assert.That(projectNames.Count(), Is.EqualTo(4));
				Assert.That(projectNames.ElementAt(0), Is.EqualTo("ProjA"));
				Assert.That(projectNames.ElementAt(1), Is.EqualTo("ProjB"));
				Assert.That(projectNames.ElementAt(2), Is.EqualTo("ProjC"));
				Assert.That(projectNames.ElementAt(3), Is.EqualTo("ProjK"));
			}
		}

		[Test]
		public void FindAllLiftUpdateFilesWithWrongNameFormat()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				//Create a .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("BlahBlah_sha45863time587" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("BlahBlahsha45863time" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha45_four_partsToName" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LiftUpdatesPath);

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				var badLiftUpdateFiles = updatesScanner.UpdateFilesWithWrongNameFormat;
				Assert.IsNotNull(badLiftUpdateFiles);
				Assert.That(badLiftUpdateFiles.Count(), Is.EqualTo(2));
				Assert.That(badLiftUpdateFiles.ElementAt(1), Is.EqualTo(Path.Combine(e.LiftUpdatesPath, "BlahBlah_sha45863time587" + SynchronicMerger.ExtensionOfIncrementalFiles)));
				Assert.That(badLiftUpdateFiles.ElementAt(0), Is.EqualTo(Path.Combine(e.LiftUpdatesPath, "BlahBlahsha45863time" + SynchronicMerger.ExtensionOfIncrementalFiles)));

				var liftUpdateFilesInfo = updatesScanner.LiftUpdateFilesInfo;
				Assert.IsNotNull(liftUpdateFilesInfo);
				Assert.That(liftUpdateFilesInfo.Count(), Is.EqualTo(3));
				VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(0), "ProjA", "sha0123", "ProjA_sha0123_uniqueExtra4578", e.LiftUpdatesPath);
				VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(1), "ProjC", "sha45863", "ProjC_sha45863_time587", e.LiftUpdatesPath);
				VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(2), "ProjC", "sha45", "ProjC_sha45_four_partsToName", e.LiftUpdatesPath);
			}
		}

		[Test]
		public void FindAllShasForParticularProjects()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				//Create a .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate1, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_uniqueExtra2459" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_unique_blurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0124_uniqffdflurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0125_uniqsdsdlurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjK_sha0123_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha0123_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate();
				Assert.IsNotNull(projectNames);
				Assert.That(projectNames.Count(), Is.EqualTo(4));
				Assert.That(projectNames.ElementAt(0), Is.EqualTo("ProjA"));
				Assert.That(projectNames.ElementAt(1), Is.EqualTo("ProjB"));
				Assert.That(projectNames.ElementAt(2), Is.EqualTo("ProjC"));
				Assert.That(projectNames.ElementAt(3), Is.EqualTo("ProjK"));

				var shasForProjectName = updatesScanner.GetShasForAProjectName("ProjA");
				Assert.IsNotNull(shasForProjectName);
				Assert.That(shasForProjectName.Count(), Is.EqualTo(3));
				Assert.That(shasForProjectName.ElementAt(0), Is.EqualTo("sha0123"));
				Assert.That(shasForProjectName.ElementAt(1), Is.EqualTo("sha0124"));
				Assert.That(shasForProjectName.ElementAt(2), Is.EqualTo("sha0125"));

				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjB");
				Assert.IsNotNull(shasForProjectName);
				Assert.That(shasForProjectName.Count(), Is.EqualTo(1));
				Assert.That(shasForProjectName.ElementAt(0), Is.EqualTo("sha3456"));

				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjC");
				Assert.IsNotNull(shasForProjectName);
				Assert.That(shasForProjectName.Count(), Is.EqualTo(2));
				Assert.That(shasForProjectName.ElementAt(0), Is.EqualTo("sha0123"));
				Assert.That(shasForProjectName.ElementAt(1), Is.EqualTo("sha45863"));


				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjK");
				Assert.IsNotNull(shasForProjectName);
				Assert.That(shasForProjectName.Count(), Is.EqualTo(1));
				Assert.That(shasForProjectName.ElementAt(0), Is.EqualTo("sha0123"));
			}
		}


		[Test]
		public void GetFullFilePathsForAProjectAndShaInTimeStampOrder()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateUpdateFolder();

				//Create a .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);

				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_D" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate1, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_A" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0123_C" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0124_uniqffdflurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjA_sha0125_uniqsdsdlurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);


				LfSynchronicMergerTests.WriteFile("ProjC_sha45863_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha0123_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjK_sha0123_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);
				LfSynchronicMergerTests.WriteFile("ProjC_sha45863_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles,
												  s_LiftUpdate2, e.LiftUpdatesPath);

				var updatesScanner = new LiftUpdatesScanner(e.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate();
				Assert.IsNotNull(projectNames);
				Assert.That(projectNames.Count(), Is.EqualTo(4));
				Assert.That(projectNames.ElementAt(0), Is.EqualTo("ProjA"));
				Assert.That(projectNames.ElementAt(1), Is.EqualTo("ProjB"));
				Assert.That(projectNames.ElementAt(2), Is.EqualTo("ProjC"));
				Assert.That(projectNames.ElementAt(3), Is.EqualTo("ProjK"));

				var liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0123");
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Count(), Is.EqualTo(3));
				Assert.That(liftUpdateFilesToApply.ElementAt(0), Is.EqualTo(LiftUpdateFileFullPath("ProjA_sha0123_D", e)));
				Assert.That(liftUpdateFilesToApply.ElementAt(1), Is.EqualTo(LiftUpdateFileFullPath("ProjA_sha0123_A", e)));
				Assert.That(liftUpdateFilesToApply.ElementAt(2), Is.EqualTo(LiftUpdateFileFullPath("ProjA_sha0123_C", e)));

				liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0124");
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Count(), Is.EqualTo(1));
				Assert.That(liftUpdateFilesToApply.ElementAt(0), Is.EqualTo(LiftUpdateFileFullPath("ProjA_sha0124_uniqffdflurp9", e)));

				liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0125");
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Count(), Is.EqualTo(1));
				Assert.That(liftUpdateFilesToApply.ElementAt(0), Is.EqualTo(LiftUpdateFileFullPath("ProjA_sha0125_uniqsdsdlurp9", e)));


			}
		}

		private string LiftUpdateFileFullPath(String filename, TestEnvironment e)
		{
			return Path.Combine(e.LiftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles);
		}
	}
}