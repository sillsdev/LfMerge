using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.TestUtilities;

namespace lfmergelift.Tests
{
	[TestFixture]
	public class ProjectUpdateFolderTests
	{
		class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _folder = new TemporaryFolder("ProjectUpdateFolderTests");

			public void CreateUpdateFile(string projectName, string sha, string updateFileNameWithoutExtension)
			{

			}

			public string BasePath
			{
				get { return _folder.Path; }
			}

			public string UpdateRoot
			{
				get { return Path.Combine(BasePath, "updates"); }
			}

			public string ProjectUpdatePath(string projectName)
			{
				return Path.Combine(UpdateRoot, projectName);
			}

			public string UpdatePath(string projectName, string sha)
			{
				return Path.Combine(ProjectUpdatePath(projectName), sha);
			}

			public string UpdateFilePath(string projectName, string sha, string fileNameWithoutExtension)
			{
				return Path.Combine(UpdatePath(projectName, sha), fileNameWithoutExtension + Extension);
			}

			private static string Extension
			{
				get { return SynchronicMerger.ExtensionOfIncrementalFiles; }
			}

			private string ContentLiftOneEntry()
			{
				return
@"<?xml version='1.0' encoding='utf-8'?>
<lift
	version='0.13'
	producer='Palaso.DictionaryServices.LiftWriter 1.5.178.0'>
	<entry
		id='EntryOne_2b5632d0-c7e8-4cf1-8b8b-66272e941a74'
		dateCreated='2011-03-10T08:28:23Z'
		dateModified='2011-04-19T08:58:06Z'
		guid='2b5632d0-c7e8-4cf1-8b8b-66272e941a74'>
		<lexical-unit>
			<form
				lang='en'>
				<text>EntryOne</text>
			</form>
		</lexical-unit>
	</entry>
</lift>
".Replace('\'', '"');
			}

			public void CreateUpdateFile(string projectName, string sha, string fileNameWithoutExtension, string content)
			{
				File.WriteAllText(UpdateFilePath(projectName, sha, fileNameWithoutExtension), content);
			}

			public void Dispose()
			{
				_folder.Dispose();
			}

		}

		[Test]
		public void Path_Correct()
		{
			using (var e = new TestEnvironment())
			{
				// env needs to create directories
				// env needs to set the work dir in the test env on the ProjectUpdateFolder
				var project = e.CreateProjectUpdateFolder();

			}
		}

	}
}
