// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using IniParser.Parser;
using NUnit.Framework;
using Palaso.CommandLineProcessing;
using Palaso.Progress;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests
{
	public static class MercurialTestHelper
	{
		public static void InitializeHgRepo(string projectFolderPath)
		{
			Directory.CreateDirectory(projectFolderPath);
			var result = CommandLineRunner.Run("hg", "init .", projectFolderPath, 120,
				new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg init . failed.\nStdOut: {0}\nStdErr: {1}",
				result.StandardOutput, result.StandardError));

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
			var result = CommandLineRunner.Run("hg", string.Format("add {0}", file),
				repoPath, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg add {0} failed.\nStdOut: {1}\nStdErr: {2}", file,
				result.StandardOutput, result.StandardError));
		}

		public static void HgCommit(string repoPath, string message)
		{
			var result = CommandLineRunner.Run("hg",
				string.Format("commit -A -u dummyUser -m \"{0}\"", message), repoPath,
				120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("commit -A -u dummyUser -m \"{0}\" failed.\nStdOut: {1}\nStdErr: {2}",
					message, result.StandardOutput, result.StandardError));
		}

		public static void HgCreateBranch(string repoPath, string branchName)
		{
			var result = CommandLineRunner.Run("hg",
				string.Format("branch -f \"{0}\"", branchName), repoPath, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg branch -f \"{0}\" failed.\nStdOut: {1}\nStdErr: {2}", branchName,
					result.StandardOutput, result.StandardError));
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
			var result = CommandLineRunner.Run("hg",
				string.Format("clone {0} .", sourceRepo), destinationRepo, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg clone {0} . failed.\nStdOut: {1}\nStdErr: {2}", sourceRepo,
					result.StandardOutput, result.StandardError));
		}

		public static void ChangeBranch(string repoPath, string newBranch)
		{
			var result = CommandLineRunner.Run("hg",
				string.Format("update {0}", newBranch), repoPath, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg update {0} failed.\nStdOut: {1}\nStdErr: {2}", newBranch,
					result.StandardOutput, result.StandardError));
		}

	}
}

