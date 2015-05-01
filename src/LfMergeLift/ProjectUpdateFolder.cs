// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LfMergeLift
{
	public class ProjectUpdateFolder
	{
		public ProjectUpdateFolder(DirectoryInfo directoryInfo)
		{
			BaseDirectoryInfo = directoryInfo;
		}

		private DirectoryInfo BaseDirectoryInfo { get; set; }

		public string Path
		{
			get { return BaseDirectoryInfo.FullName; }
		}

		public IEnumerable<UpdateFolder> FindUpdateFolders()
		{
			var subDirectories = BaseDirectoryInfo.GetDirectories();
			Array.Sort(subDirectories, new FolderScanner.DirectoryInfoLastWriteTimeComparer());
			foreach (var subDirectory in subDirectories)
			{
				yield return new UpdateFolder(subDirectory);
			}
		}

		public void ApplyUpdatesInFolder(UpdateFolder updateFolder)
		{
			// Update the working folder to this SHA

			// Copy all the updates into the working folder

			// Use synchronicmerger to apply the updates.

			// Do a chorus merge

			// Remove / record the updates applied
		}

		public void PushToMaster()
		{

		}

		public void UpdateCurrent()
		{

		}


	}
}
