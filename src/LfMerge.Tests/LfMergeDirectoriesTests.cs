// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using LfMerge;
using NUnit.Framework;
using Palaso.TestUtilities;

namespace LfMerge.Tests
{
	[TestFixture]
	public class LfMergeDirectoriesTests
	{
		[Test]
		public void FdoDirs_RelativePathsAreSubdirsOfBasedir()
		{
			// SUT
			var fdoDirs = new LfMergeDirectories("/tmp", "projects", "templates");

			Assert.That(fdoDirs.ProjectsDirectory, Is.EqualTo("/tmp/projects"));
			Assert.That(fdoDirs.DefaultProjectsDirectory, Is.EqualTo("/tmp/projects"));
			Assert.That(fdoDirs.TemplateDirectory, Is.EqualTo("/tmp/templates"));
		}

		[Test]
		public void FdoDirs_AbsolutePathsRemainAbsolute()
		{
			// SUT
			var fdoDirs = new LfMergeDirectories("/tmp", "/projects", "/foo/templates");

			Assert.That(fdoDirs.ProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(fdoDirs.DefaultProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(fdoDirs.TemplateDirectory, Is.EqualTo("/foo/templates"));
		}

		[Test]
		public void StateDirectory_Correct()
		{
			var dirs = new LfMergeDirectories(Path.GetTempPath());
			Assert.That(dirs.StateDirectory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "state")));
		}

		[Test]
		public void GetStateFileName_Correct()
		{
			// Setup
			using (var temp = new TemporaryFolder("StateFile"))
			{
				var dirs = new LfMergeDirectories(temp.Path);

				// SUT
				var stateFile = dirs.GetStateFileName("ProjA");

				// Verify
				Assert.That(stateFile, Is.EqualTo(Path.Combine(temp.Path, "state/ProjA.state")));
				Assert.That(Directory.Exists(Path.GetDirectoryName(stateFile)), Is.True,
					"State directory didn't get created");
			}
		}
	}
}

