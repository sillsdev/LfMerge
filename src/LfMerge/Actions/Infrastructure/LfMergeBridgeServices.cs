// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

		public static string NumEntries(int count, string after)
		{
			if (count <= 0)
				return String.Empty;
			return String.Format("{0} entr{1} {2}", count, (count == 1) ? "y" : "ies", after);
		}

		public static string FormatCommitMessageForLfMerge(int entriesAdded, int entriesModified, int entriesDeleted)
		{
			if (entriesAdded <= 0 && entriesModified <= 0 && entriesDeleted <= 0)
				return "Language Forge S/R";
			IEnumerable<string> commitData = new string[3] {
				NumEntries(entriesAdded,    "added"),
				NumEntries(entriesModified, "modified"),
				NumEntries(entriesDeleted,  "deleted")
			}.Where(s => !String.IsNullOrEmpty(s));
			return String.Format("Language Forge: {0}", String.Join(", ", commitData));
			// Sample output: "Language Forge: 4 entries added, 2 entries modified, 1 entry deleted".
		}
	}
}
