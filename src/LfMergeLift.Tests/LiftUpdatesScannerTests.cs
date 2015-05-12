// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.Lift.Validation;
using Palaso.TestUtilities;


namespace LfMergeLift.Tests
{
	[TestFixture]
	public class LiftUpdatesScannerTests
	{
		class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _folder = new TemporaryFolder("MergeWork");
			private const string UpdatesFolder = "liftUpdates";

			public void Dispose()
			{
				_folder.Dispose();
			}

			private string MergeWorkFolder
			{
				get { return _folder.Path; }
			}

			public string LiftUpdatesPath
			{
				get { return Path.Combine(MergeWorkFolder, UpdatesFolder); }
			}

			public void CreateUpdateFolder()
			{
				Directory.CreateDirectory(LiftUpdatesPath);
			}

			internal void WriteFile(string fileName, string xmlForEntries, string directory)
			{
				StreamWriter writer = File.CreateText(Path.Combine(directory, fileName));
				string content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
								 + "<lift version =\""
								 + Validator.LiftVersion
								 + "\" producer=\"WeSay.1Pt0Alpha\" xmlns:flex=\"http://fieldworks.sil.org\">"
								 + xmlForEntries
								 + "</lift>";
				writer.Write(content);
				writer.Close();
				writer.Dispose();

				//pause so they don't all have the same time
				Thread.Sleep(100);
			}

			internal string LiftUpdateFileFullPath(String filename)
			{
				return Path.Combine(LiftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles);
			}

			internal void VerifyUpdateInfoRecord(UpdateInfo updateInfoRecord, String proj, String sha, String filename, String liftUpdatesPath)
			{
				Assert.That(updateInfoRecord.Project, Is.EqualTo(proj));
				Assert.That(updateInfoRecord.Sha, Is.EqualTo(sha));
				Assert.That(updateInfoRecord.SystemFileInfo.FullName, Is.EqualTo(Path.Combine(liftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles)));
			}

		} //END class TestEnvironment
		//=============================================================================================================================

		private const string BaseLiftFileName = "base.lift";

		[Test]
		public void FindLiftUpdateFiles_TwoLiftUpdateFiles_OneExtraFile()
		{
			const string liftData1 = @"
<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>
<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>
<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>
";
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			//This test puts 3 files in the mergerWork/liftUpdates folder.  Two are .lift.updates files and one is not.
			//Verify that only the .lift.updates files are returned from the call to GetPendingUpdateFiles
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				////Create a file that is not a .lift.update file
				env.WriteFile(BaseLiftFileName, liftData1, env.LiftUpdatesPath);
				//Create a .lift.update file.
				env.WriteFile("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate1, env.LiftUpdatesPath);
				//Create another .lift.update file
				env.WriteFile("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				FileInfo[] files = LiftUpdatesScanner.GetPendingUpdateFiles(env.LiftUpdatesPath);
				//Verify results are correct
				Assert.That(files.Length, Is.EqualTo(2));
				Assert.That(files[0].Name, Is.EqualTo("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles));
				Assert.That(files[1].Name, Is.EqualTo("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles));

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);
				//Verify results are correct
				Assert.That(updatesScanner.LiftUpdateFiles.Length, Is.EqualTo(2));
				Assert.That(updatesScanner.LiftUpdateFiles[0].Name, Is.EqualTo("Proj_Sha_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles));
				Assert.That(updatesScanner.LiftUpdateFiles[1].Name, Is.EqualTo("Proj_Sha_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles));

				Assert.That(updatesScanner.ScannerHasListOfLiftUpdates, Is.True);
			}
		}

		[Test]
		public void FindLiftUpdateFiles_NoneExist()
		{
			const string liftData1 = @"
<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>
<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>
<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>
";
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				////Create a file that is not a .lift.update file
				env.WriteFile("fileOne.notLiftUPdate", liftData1, env.LiftUpdatesPath);
				env.WriteFile("fileTwo.whatever", liftData1, env.LiftUpdatesPath);
				env.WriteFile("fileTwo.LIFT", liftData1, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				FileInfo[] files = LiftUpdatesScanner.GetPendingUpdateFiles(env.LiftUpdatesPath);

				//Verify results
				Assert.That(files, Is.Empty);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);

				//Verify Results
				Assert.That(updatesScanner.LiftUpdateFiles, Is.Empty);
				Assert.That(updatesScanner.ScannerHasListOfLiftUpdates, Is.False);
			}
		}

		[Test]
		public void FindLiftUpdateFiles_AndVerifyCorrect_UpdateInfo()
		{
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";

			//This test gets a number of .lift.update files and verifies that the UpdateInfo structure was correctly built
			//Each .lift.update file will have the following format.
			// projX_sha#_timeStamp.lift.update
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				//Create a .lift.update file
				env.WriteFile("ProjA_sha0123_uniqueExtra1" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate1, env.LiftUpdatesPath);
				//Create a second .lift.update file.
				env.WriteFile("ProjA_sha0123_uniqueExtra2" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);

				//Verify Results
				var liftUpdateFilesInfo = updatesScanner.LiftUpdateFilesInfo.ToArray();
				Assert.IsNotNull(liftUpdateFilesInfo);
				var updateInfoToCheck = (from updateInfo in liftUpdateFilesInfo
											where updateInfo.Project == "ProjA"
											select updateInfo).ToArray();
				Assert.That(updateInfoToCheck.Count(), Is.EqualTo(2));
				env.VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(0), "ProjA", "sha0123", "ProjA_sha0123_uniqueExtra1", env.LiftUpdatesPath);
				env.VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(1), "ProjA", "sha0123", "ProjA_sha0123_uniqueExtra2", env.LiftUpdatesPath);

				updateInfoToCheck = (from updateInfo in liftUpdateFilesInfo
										where updateInfo.Project == "ProjB"
										select updateInfo).ToArray();
				Assert.That(updateInfoToCheck.Count(), Is.EqualTo(1));
				env.VerifyUpdateInfoRecord(updateInfoToCheck.ElementAt(0), "ProjB", "sha3456", "ProjB_sha3456_time11", env.LiftUpdatesPath);
			}
		}

		[Test]
		public void FindAllProjectNamesThatHaveLiftUpdates()
		{
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				//Create a .lift.update file
				env.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate1, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0123_uniqueExtra2459" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjK_sha45874563_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate();

				//Verify Results
				Assert.That(projectNames, Is.EqualTo(new[] { "ProjA", "ProjB", "ProjC", "ProjK" }));
			}
		}

		[Test]
		public void FindAllLiftUpdateFilesWithWrongNameFormat()
		{
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var testEnv = new TestEnvironment())
			{
				//Setup for tests
				testEnv.CreateUpdateFolder();
				//Create a .lift.update file
				testEnv.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate1, testEnv.LiftUpdatesPath);
				testEnv.WriteFile("BlahBlah_sha45863time587" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, testEnv.LiftUpdatesPath);
				testEnv.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, testEnv.LiftUpdatesPath);
				testEnv.WriteFile("BlahBlahsha45863time" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, testEnv.LiftUpdatesPath);
				testEnv.WriteFile("ProjC_sha45_four_partsToName" + SynchronicMerger.ExtensionOfIncrementalFiles, liftUpdate2, testEnv.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(testEnv.LiftUpdatesPath);
				var badLiftUpdateFiles = updatesScanner.UpdateFilesWithWrongNameFormat;
				//Verify Results
				Assert.That(badLiftUpdateFiles, Is.EquivalentTo(new[]
				{
					Path.Combine(testEnv.LiftUpdatesPath, "BlahBlah_sha45863time587" + SynchronicMerger.ExtensionOfIncrementalFiles),
					Path.Combine(testEnv.LiftUpdatesPath, "BlahBlahsha45863time" + SynchronicMerger.ExtensionOfIncrementalFiles)
				}));

				var liftUpdateFilesInfo = updatesScanner.LiftUpdateFilesInfo.ToArray();
				Assert.IsNotNull(liftUpdateFilesInfo);
				Assert.That(liftUpdateFilesInfo.Count(), Is.EqualTo(3));
				testEnv.VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(0),
					"ProjA", "sha0123", "ProjA_sha0123_uniqueExtra4578", testEnv.LiftUpdatesPath);
				testEnv.VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(1),
					"ProjC", "sha45863", "ProjC_sha45863_time587", testEnv.LiftUpdatesPath);
				testEnv.VerifyUpdateInfoRecord(liftUpdateFilesInfo.ElementAt(2),
					"ProjC", "sha45", "ProjC_sha45_four_partsToName", testEnv.LiftUpdatesPath);
			}
		}

		[Test]
		public void FindAllShasForParticularProjects()
		{
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				//Create a .lift.update file
				env.WriteFile("ProjA_sha0123_uniqueExtra4578" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate1, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0123_uniqueExtra2459" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0123_unique_blurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0124_uniqffdflurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0125_uniqsdsdlurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjK_sha0123_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjC_sha45863_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjC_sha0123_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate();
				//Verify Results
				Assert.That(projectNames, Is.EqualTo(new[] { "ProjA", "ProjB", "ProjC", "ProjK" }));

				var shasForProjectName = updatesScanner.GetShasForAProjectName("ProjA");
				Assert.That(shasForProjectName, Is.EqualTo(new[] {"sha0123", "sha0124", "sha0125"}));

				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjB");
				Assert.That(shasForProjectName, Is.EqualTo(new[] { "sha3456" }));

				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjC");
				Assert.That(shasForProjectName, Is.EqualTo(new[] { "sha0123", "sha45863"}));


				shasForProjectName = updatesScanner.GetShasForAProjectName("ProjK");
				Assert.That(shasForProjectName, Is.EqualTo(new[] { "sha0123" }));
			}
		}

		[Test]
		public void GetFullFilePathsForAProjectAndShaInTimeStampOrder()
		{
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				//Setup for tests
				env.CreateUpdateFolder();
				//Create a .lift.update file
				env.WriteFile("ProjB_sha3456_time11" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);

				env.WriteFile("ProjA_sha0123_D" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate1, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0123_A" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0123_C" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0124_uniqffdflurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjA_sha0125_uniqsdsdlurp9" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);


				env.WriteFile("ProjC_sha45863_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjC_sha0123_time587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjK_sha0123_time114587" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);
				env.WriteFile("ProjC_sha45863_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdate2, env.LiftUpdatesPath);

				//Run LiftUpdatesScanner
				var updatesScanner = new LiftUpdatesScanner(env.LiftUpdatesPath);
				var projectNames = updatesScanner.GetProjectsNamesToUpdate().ToArray();
				//Verify Results
				Assert.That(projectNames, Is.EqualTo(new[] { "ProjA", "ProjB", "ProjC", "ProjK" }));

				//Run LiftUpdatesScanner
				var liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0123");
				//Verify Results
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Select(s => s.FullName), Is.EquivalentTo(new[] {
					env.LiftUpdateFileFullPath("ProjA_sha0123_A"),
					env.LiftUpdateFileFullPath("ProjA_sha0123_C"),
					env.LiftUpdateFileFullPath("ProjA_sha0123_D") }));

				//Run LiftUpdatesScanner
				var liftUpdateFilesArrayToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0123");
				//Verify Results
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesArrayToApply.Select(s => s.FullName), Is.EquivalentTo(new[] {
					env.LiftUpdateFileFullPath("ProjA_sha0123_A"),
					env.LiftUpdateFileFullPath("ProjA_sha0123_C"),
					env.LiftUpdateFileFullPath("ProjA_sha0123_D") }));

				//Run LiftUpdatesScanner
				liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0124");
				//Verify Results
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Select(s => s.FullName), Is.EquivalentTo(new[] {
					env.LiftUpdateFileFullPath("ProjA_sha0124_uniqffdflurp9") }));

				//Run LiftUpdatesScanner
				liftUpdateFilesToApply = updatesScanner.GetUpdateFilesForProjectAndSha("ProjA", "sha0125");
				//Verify Results
				Assert.IsNotNull(liftUpdateFilesToApply);
				Assert.That(liftUpdateFilesToApply.Select(s => s.FullName), Is.EquivalentTo(new[] {
					env.LiftUpdateFileFullPath("ProjA_sha0125_uniqsdsdlurp9") }));
			}
		}
	}
}