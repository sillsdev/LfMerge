// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using LfMerge;

namespace LfMergeFdo.Tests
{
	[TestFixture]
	public class FdoDirectoriesTests
	{
		[Test]
		public void RelativePaths_AreSubdirsOfBasedir()
		{
			// SUT
			var fdoDirs = new FdoDirectories("/tmp/", "projects", "templates");

			Assert.That(fdoDirs.ProjectsDirectory, Is.EqualTo("/tmp/projects"));
			Assert.That(fdoDirs.DefaultProjectsDirectory, Is.EqualTo("/tmp/projects"));
			Assert.That(fdoDirs.TemplateDirectory, Is.EqualTo("/tmp/templates"));
		}

		[Test]
		public void AbsolutePaths_RemainAbsolute()
		{
			// SUT
			var fdoDirs = new FdoDirectories("/tmp/", "/projects", "/foo/templates");

			Assert.That(fdoDirs.ProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(fdoDirs.DefaultProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(fdoDirs.TemplateDirectory, Is.EqualTo("/foo/templates"));
		}
	}
}

