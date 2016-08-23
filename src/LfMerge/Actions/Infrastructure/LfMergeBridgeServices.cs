// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Actions.Infrastructure
{
	public static class LfMergeBridgeServices
	{
		private static IList<string> GetLinesFromLfBridge(string somethingForClient)
		{
			return somethingForClient.Split(new[] {"\n", "\r"}, StringSplitOptions.RemoveEmptyEntries).ToList();
		}

		public static string GetLineContaining(string somethingForClient, string searchedFor)
		{
			foreach (var line in GetLinesFromLfBridge(somethingForClient))
			{
				if (line.Contains(searchedFor))
				{
					return line;
				}
			}
			return string.Empty;
		}

		public static string FormatCommitMessageForLfMerge(int entriesAdded, int entriesModified, int entriesDeleted)
		{
			if (entriesAdded == 0 && entriesModified == 0 && entriesDeleted == 0)
				return "Language Forge Send/Receive, with no changes from previous Send/Receive.";
			// TODO: Any other text that we want in the commit message?
			return String.Format("Language Forge Send/Receive. Compared to the previous Send/Receive, {0} entries were added, {1} entries were modified, and {2} entries were deleted.",
				entriesAdded, entriesModified, entriesDeleted);
		}
	}
}
