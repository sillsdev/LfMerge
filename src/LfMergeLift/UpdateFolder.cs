// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LfMergeLift
{
	public class UpdateFolder
	{
		public UpdateFolder(DirectoryInfo directoryInfo)
		{
			BaseDirectoryInfo = directoryInfo;
		}

		private DirectoryInfo BaseDirectoryInfo { get; set; }

		public string SHA
		{
			get { return BaseDirectoryInfo.Name; }
		}

		public string Path
		{
			get { return BaseDirectoryInfo.FullName; }
		}

		public IEnumerable<FileInfo> GetUpdateFiles()
		{
			var files = BaseDirectoryInfo.GetFiles();
			foreach (var fileInfo in files)
			{
				yield return fileInfo;
			}
		}


	}
}
