// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using IniParser.Parser;
using NUnit.Framework;
using Palaso.CommandLineProcessing;
using Palaso.Progress;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests
{
	public static class MercurialTestHelper
	{
		private static string RunHgCommand(string repoPath, string args)
		{
			var result = CommandLineRunner.Run("hg", args, repoPath, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0), string.Format("hg {0} failed.\nStdOut: {1}\nStdErr: {2}", args, result.StandardOutput, result.StandardError));
			return result.StandardOutput;
		}

		public static void InitializeHgRepo(string projectFolderPath)
		{
			Directory.CreateDirectory(projectFolderPath);
			RunHgCommand(projectFolderPath, "init .");

			// We have to copy the ini file, otherwise Mercurial won't accept branches that
			// consists only of digits.
			string hgini = Path.Combine(TestEnvironment.FindGitRepoRoot(), "Mercurial", "mercurial.ini");
			var hgrc = Path.Combine(projectFolderPath, ".hg", "hgrc");
			File.Copy(hgini, hgrc);

			// Adjust hgrc file
			var parser = new IniDataParser();
			var iniData = parser.Parse(File.ReadAllText(hgrc));

			iniData["extensions"].AddKey("fixutf8", Path.Combine(TestEnvironment.FindGitRepoRoot(),
				"MercurialExtensions/fixutf8/fixutf8.py"));

			var contents = iniData.ToString();
			File.WriteAllText(hgrc, contents);
		}

		public static void HgAddFile(string repoPath, string file)
		{
			RunHgCommand(repoPath, string.Format("add {0}", file));
		}

		public static void HgCommit(string repoPath, string message)
		{
			RunHgCommand(repoPath, string.Format("commit -A -u dummyUser -m \"{0}\"", message));
		}

		public static void HgCreateBranch(string repoPath, string branchName)
		{
			RunHgCommand(repoPath, string.Format("branch -f \"{0}\"", branchName));
		}

		public static void CreateFlexRepo(string lDProjectFolderPath, string modelVersion = null)
		{
			if (string.IsNullOrEmpty(modelVersion))
				modelVersion = FdoCache.ModelVersion;
			File.WriteAllText(Path.Combine(lDProjectFolderPath, "FLExProject.CustomProperties"),
				"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AdditionalFields/>");
			File.WriteAllText(Path.Combine(lDProjectFolderPath, "FLExProject.ModelVersion"),
				string.Format("{{\"modelversion\": {0}}}", modelVersion));

			HgCommit(lDProjectFolderPath, "Initial commit");
		}

		public static void CloneRepo(string sourceRepo, string destinationRepo)
		{
			Directory.CreateDirectory(destinationRepo);
			RunHgCommand(destinationRepo, string.Format("clone {0} .", sourceRepo));
		}

		public static void ChangeBranch(string repoPath, string newBranch)
		{
			RunHgCommand(repoPath, string.Format("update {0}", newBranch));
		}

		public static string GetRevisionOfWorkingSet(string repoPath)
		{
			return RunHgCommand(repoPath, "parents --template \"{node|short}\"");
		}

		public static string GetRevisionOfTip(string repoPath)
		{
			return RunHgCommand(repoPath, "tip --template \"{node|short}\"");
		}
	}
}

