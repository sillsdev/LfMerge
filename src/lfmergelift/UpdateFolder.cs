using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace lfmergelift
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
