// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using SIL.LCModel;

namespace LfMerge.Core.FieldWorks
{
	class ProjectIdentifier: IProjectIdentifier
	{
		public ProjectIdentifier(ILcmDirectories LcmDirs, string database)
		{
			LcmDirectories = LcmDirs;
			Path = GetPathToDatabase(LcmDirs, database);
		}

		private static string GetPathToDatabase(ILcmDirectories LcmDirs, string database)
		{
			return System.IO.Path.Combine(LcmDirs.ProjectsDirectory, database,
				database + LcmFileHelper.ksFwDataXmlFileExtension);
		}

		public ILcmDirectories LcmDirectories { get; private set; }

		#region IProjectIdentifier implementation

		public bool IsLocal
		{
			get { return true; }
		}

		public string Path { get; set; }

		public string ProjectFolder
		{
			get { return System.IO.Path.GetDirectoryName(Path); }
		}

		public string SharedProjectFolder
		{
			get { return ProjectFolder; }
		}

		public string ServerName
		{
			get { return null; }
		}

		public string Handle
		{
			get { return Name; }
		}

		public string PipeHandle
		{
			get { throw new NotSupportedException(); }
		}

		public string Name
		{
			get { return System.IO.Path.GetFileNameWithoutExtension(Path); }
		}

		public BackendProviderType Type { get { return BackendProviderType.kXML; } }

		public string UiName
		{
			get { return Name; }
		}
		#endregion
	}
}
