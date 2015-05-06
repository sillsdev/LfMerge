// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.Lift.Validation;
using Palaso.TestUtilities;

namespace LfMergeLift.Tests
{
	[TestFixture]
	public class FolderScannerTests
	{
		class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _folder = new TemporaryFolder("FolderScannerTests");

			public void Dispose()
			{
				_folder.Dispose();
			}

			public string Path
			{
				get { return _folder.Path; }
			}

			public string ProjectPath(string projectName)
			{
				return System.IO.Path.Combine(Path, projectName);
			}

			public void CreateProjectUpdateFolder(string ProjectName)
			{
				Directory.CreateDirectory(ProjectPath(ProjectName));
			}

			internal string WriteFile(string fileName, string xmlForEntries, string directory)
			{
				StreamWriter writer = File.CreateText(System.IO.Path.Combine(directory, fileName));
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

				return content;
			}

		} //END class TestEnvironment
		//=============================================================================================================================

		[Test]
		public void FindProjectFolders_ZeroProjects_FindsZero()
		{
			using (var e = new TestEnvironment())
			{
				var scanner = new FolderScanner(e.Path);
				var updateFoldersList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFoldersList.Count, Is.EqualTo(0));
			}
		}

		[Test]
		public void FindProjectFolders_OneProject_FindsOne()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("1");
				var scanner = new FolderScanner(e.Path);
				var updateFoldersList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFoldersList.Count, Is.EqualTo(1));
			}

		}

		[Test]
		public void FindProjectFolders_OneProject_ProjectUpdateFolderHasCorrectFilePath()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("1");
				var scanner = new FolderScanner(e.Path);
				var updateFoldersList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFoldersList[0].Path, Is.EqualTo(e.ProjectPath("1")));
			}
		}

		[Test]
		public void FindProjectFolders_OneProject_TwoUpdateFolders()
		{
			//create update folders so that the alphabetical ordering on the folders differs from the time stamps.
			//we want to see if the FolderScanner returns the folders in the correct order from time stamps
			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("2FirstFolderCreated");
				Thread.Sleep(1000);
				e.CreateProjectUpdateFolder("1SecondFolderCreated");
				var scanner = new FolderScanner(e.Path);
				var updateFoldersList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFoldersList.Count, Is.EqualTo(2));
				Assert.That(updateFoldersList[0].Path, Is.EqualTo(e.ProjectPath("2FirstFolderCreated")));
				Assert.That(updateFoldersList[1].Path, Is.EqualTo(e.ProjectPath("1SecondFolderCreated")));
			}
		}

		private const string _baseLiftFileName = "base.lift";

		[Test]
		public void FindProjectFolders_OneProject_TwoUpdateFolders_OneWithTwoLiftUpdateFiles()
		{
			string s_LiftData1 =
				"<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
				+ "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>"
				+ "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

			string s_LiftUpdate1 =
				"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
				+ "<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>"
				+ "<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>";

			string s_LiftUpdate2 =
				"<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
				+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";

			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("2FirstFolderCreated");
				Thread.Sleep(1000);
				e.CreateProjectUpdateFolder("1SecondFolderCreated");

				var scanner = new FolderScanner(e.Path);
				var updateFoldersList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFoldersList.Count, Is.EqualTo(2));
				Assert.That(updateFoldersList[0].Path, Is.EqualTo(e.ProjectPath("2FirstFolderCreated")));
				Assert.That(updateFoldersList[1].Path, Is.EqualTo(e.ProjectPath("1SecondFolderCreated")));

				////Create a LIFT file with 3 entries which will have updates applied to it.
				e.WriteFile(_baseLiftFileName, s_LiftData1, e.ProjectPath("1SecondFolderCreated"));
				//Create a .lift.update file with three entries.  One to replace the second entry in the original LIFT file.
				//The other two are new and should be appended to the original LIFT file.
				e.WriteFile("LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, e.ProjectPath("1SecondFolderCreated"));
				//Create a .lift.update file with two entries.  One to replace one of the changes from the first LiftUpdate file and one new entry.
				e.WriteFile("LiftChangeFileA" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.ProjectPath("1SecondFolderCreated"));
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(e.ProjectPath("1SecondFolderCreated"), _baseLiftFileName));
				Assert.That(files.Length, Is.EqualTo(2));
				Assert.That(files[0].Name, Is.EqualTo("LiftChangeFileA" + SynchronicMerger.ExtensionOfIncrementalFiles));
				Assert.That(files[1].Name, Is.EqualTo("LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles));
			}
		}
	}
}
