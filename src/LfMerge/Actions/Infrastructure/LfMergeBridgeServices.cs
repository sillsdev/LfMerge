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
	}
}
