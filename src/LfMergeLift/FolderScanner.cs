using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LfMergeLift
{
	public class FolderScanner
	{
		public FolderScanner(string basePath)
		{
			BasePath = basePath;
			SearchPattern = Palaso.Lift.Merging.SynchronicMerger.ExtensionOfIncrementalFiles;
		}

		public string BasePath { get; private set; }

		public string SearchPattern { get; private set; }

		public IEnumerable<ProjectUpdateFolder> FindProjectFolders()
		{
			var directoryInfo = new DirectoryInfo(BasePath);
			var subDirectories = directoryInfo.GetDirectories();
			Array.Sort(subDirectories, new DirectoryInfoLastWriteTimeComparer());
			foreach (var subDirectory in subDirectories)
			{
				yield return new ProjectUpdateFolder(subDirectory);
			}
		}


		internal class DirectoryInfoLastWriteTimeComparer : IComparer<DirectoryInfo>
		{
			public int Compare(DirectoryInfo x, DirectoryInfo y)
			{
				int timecomparison = DateTime.Compare(x.LastWriteTimeUtc, y.LastWriteTimeUtc);
				if (timecomparison == 0)
				{
					// if timestamps are the same, then sort by name
					return StringComparer.OrdinalIgnoreCase.Compare(x.FullName, y.FullName);
				}
				return timecomparison;
			}
		}


	}
}
