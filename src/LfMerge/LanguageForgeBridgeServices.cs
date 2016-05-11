// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Palaso.IO;

namespace LfMerge
{
	public static class LanguageForgeBridgeServices
	{
		public static IList<string> GetLinesFromLfBridge(string somethingForClient)
		{
			return somethingForClient.Split(new[] {"\n", "\r"}, StringSplitOptions.RemoveEmptyEntries).ToList();
		}

		public static string GetLineFromLfBridge(string somethingForClient, string searchedForLine)
		{
			foreach (var line in GetLinesFromLfBridge(somethingForClient))
			{
				if (line == searchedForLine)
				{
					return line;
				}
			}
			return string.Empty;
		}

		public static string GetLineStartingWith(string somethingForClient, string searchedForLineStarter)
		{
			foreach (var line in GetLinesFromLfBridge(somethingForClient))
			{
				if (line.StartsWith(searchedForLineStarter))
				{
					return line;
				}
			}
			return string.Empty;
		}

		public static bool CanDeleteCloneFolderCandidate(string cloneCandidateFolder)
		{
			return Directory.Exists(cloneCandidateFolder) // It does exist
				&& (DirectoryUtilities.GetSafeDirectories(cloneCandidateFolder).Length == 0)
				&& (Directory.GetFiles(cloneCandidateFolder).Length == 0); // Has no files.
		}

		public static Tuple<bool, string, string> GetLongShaFromClient(string somethingForClient)
		{
			var onDifferentBranch = false;
			string longSha = null;
			var differentBranchName = string.Empty;
			var line = GetLineStartingWith(somethingForClient, "Desired branch was");
			if (!string.IsNullOrEmpty(line))
			{
				// Oops. A different branch than expected.
				var lineChunks = line.Split(new[] { ":", "," }, StringSplitOptions.RemoveEmptyEntries);
				onDifferentBranch = true;
				differentBranchName = lineChunks[0].Trim();
				longSha = lineChunks[1].Trim();
			}
			else
			{
				line = GetLineStartingWith(somethingForClient, "New long SHA");
				if (string.IsNullOrEmpty(line))
				{
					return null; // Bad news!
				}
				longSha = line.Split(new[] {":"}, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
			}

			return Tuple.Create(onDifferentBranch, differentBranchName, longSha);
		}
	}
}
