// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using IniParser.Parser;
using NUnit.Framework;
using SIL.CommandLineProcessing;
using SIL.LCModel;
using SIL.PlatformUtilities;
using SIL.Progress;

namespace LfMerge.Core.Tests
{
	public static class MercurialTestHelper
	{
		public static string HgCommand =>
			Path.Combine(TestEnvironment.FindGitRepoRoot(), "Mercurial",
				Platform.IsWindows ? "hg.exe" : "hg");

		private static string RunHgCommand(string repoPath, string args)
		{
			var result = CommandLineRunner.Run(HgCommand, args, repoPath, 120, new NullProgress());
			if (result.ExitCode == 0) return result.StandardOutput;
			throw new System.Exception(
				$"hg {args} failed.\nStdOut: {result.StandardOutput}\nStdErr: {result.StandardError}");

		}

		public static void InitializeHgRepo(string projectFolderPath)
		{
			Directory.CreateDirectory(projectFolderPath);
			RunHgCommand(projectFolderPath, "init .");

			// We have to copy the ini file, otherwise Mercurial won't accept branches that
			// consists only of digits.
			var hgini = Path.Combine(TestEnvironment.FindGitRepoRoot(), "Mercurial", "mercurial.ini");
			var hgrc = Path.Combine(projectFolderPath, ".hg", "hgrc");
			File.Copy(hgini, hgrc);

			// Adjust hgrc file
			var parser = new IniDataParser {
				Configuration = { CommentString = "#" }
			};
			var iniData = parser.Parse(File.ReadAllText(hgrc));

			iniData["extensions"].AddKey("fixutf8", Path.Combine(TestEnvironment.FindGitRepoRoot(),
				"MercurialExtensions/fixutf8/fixutf8.py"));

			var contents = iniData.ToString();
			File.WriteAllText(hgrc, contents);
		}

		public static void HgAddFile(string repoPath, string file)
		{
			RunHgCommand(repoPath, $"add {file}");
		}

		public static void HgClean(string repoPath)
		{
			RunHgCommand(repoPath, $"purge --no-confirm");
		}

		public static void HgCommit(string repoPath, string message)
		{
			RunHgCommand(repoPath, $"commit -A -u dummyUser -m \"{message}\"");
		}

		public static void HgCreateBranch(string repoPath, int branchName)
		{
			RunHgCommand(repoPath, $"branch -f \"{branchName}\"");
		}

		public static void HgPush(string repoPath, string remoteUri)
		{
			RunHgCommand(repoPath, $"push {remoteUri}");
		}

		public static void CreateFlexRepo(string lDProjectFolderPath, int modelVersion = 0)
		{
			if (modelVersion <= 0)
				modelVersion = LcmCache.ModelVersion;
			File.WriteAllText(Path.Combine(lDProjectFolderPath, "FLExProject.CustomProperties"),
				"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AdditionalFields/>");
			File.WriteAllText(Path.Combine(lDProjectFolderPath, "FLExProject.ModelVersion"),
				$"{{\"modelversion\": {modelVersion}}}");

			HgCommit(lDProjectFolderPath, "Initial commit");
		}

		public static void CloneRepo(string sourceRepo, string destinationRepo)
		{
			Directory.CreateDirectory(destinationRepo);
			RunHgCommand(destinationRepo, $"clone {sourceRepo} .");
		}

		public static void CloneRepoAtRev(string sourceRepo, string destinationRepo, string rev)
		{
			Directory.CreateDirectory(destinationRepo);
			RunHgCommand(destinationRepo, $"clone {sourceRepo} -U -r {rev} .");
		}

		public static void CloneRepoAtRevnum(string sourceRepo, string destinationRepo, int revnum)
		{
			CloneRepoAtRev(sourceRepo, destinationRepo, revnum.ToString());
		}

		public static void ChangeBranch(string repoPath, string newBranch)
		{
			RunHgCommand(repoPath, $"update {newBranch}");
		}

		public static string GetRevisionOfWorkingSet(string repoPath)
		{
			return RunHgCommand(repoPath, "parents --template \"{node|short}\"");
		}

		public static string GetRevisionOfTip(string repoPath)
		{
			return RunHgCommand(repoPath, "tip --template \"{node|short}\"");
		}

		public static string GetUsernameFromHgrc(string repoPath)
		{
			var hgrc = Path.Combine(repoPath, ".hg", "hgrc");
			var parser = new IniDataParser();
			var iniData = parser.Parse(File.ReadAllText(hgrc));
			return iniData["ui"]["username"];
		}

	}
}

