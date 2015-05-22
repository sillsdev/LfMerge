// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using NUnit.Framework;

namespace LfMergeLift.Tests
{
	[TestFixture]
	public class ProcessingStateTests
	{
		[Test]
		public void LfMergeStateDirectory_Correct()
		{
			using (var env = new TestEnvironment())
			{
				Assert.That(env.LangForgeDirFinder.StatePath,
					Is.EqualTo(Path.Combine(env.LanguageForgeFolder, "state")));
			}
		}

		[Test]
		public void ProjectStateFile_Correct()
		{
			using (var env = new TestEnvironment())
			{
				Assert.That(env.LangForgeDirFinder.LfMergeStateFile("ProjA"),
					Is.EqualTo(Path.Combine(env.LanguageForgeFolder, Path.Combine("state", "ProjA.state"))));
			}
		}
	}
}

