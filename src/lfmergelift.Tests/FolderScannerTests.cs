using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Palaso.TestUtilities;

namespace lfmergelift.Tests
{
	[TestFixture]
	public class FolderScannerTests
	{
		class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _folder = new TemporaryFolder("FolderScannerTests");

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

			public void Dispose()
			{
				_folder.Dispose();
			}

		}

		[Test]
		public void FindProjectFolders_ZeroProjects_FindsZero()
		{
			using (var e = new TestEnvironment())
			{
				var scanner = new FolderScanner(e.Path);
				var updateFilesList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFilesList.Count, Is.EqualTo(0));
			}
		}

		[Test]
		public void FindProjectFolders_OneProject_FindsOne()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("1");
				var scanner = new FolderScanner(e.Path);
				var updateFilesList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFilesList.Count, Is.EqualTo(1));
			}

		}

		[Test]
		public void FindProjectFolders_OneProject_ProjectUpdateFolderHasCorrectFilePath()
		{
			using (var e = new TestEnvironment())
			{
				e.CreateProjectUpdateFolder("1");
				var scanner = new FolderScanner(e.Path);
				var updateFilesList = new List<ProjectUpdateFolder>(scanner.FindProjectFolders());
				Assert.That(updateFilesList[0].Path, Is.EqualTo(e.ProjectPath("1")));
			}
		}


	}
}
