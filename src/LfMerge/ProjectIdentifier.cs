// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public class ProjectIdentifier: IProjectIdentifier
	{
		public ProjectIdentifier(IFdoDirectories fdoDirs, string database)
		{
			FdoDirectories = fdoDirs;
			Type = FDOBackendProviderType.kSharedXML;
			Path = GetPathToDatabase(fdoDirs, database);
		}

		private static string GetPathToDatabase(IFdoDirectories fdoDirs, string database)
		{
			return System.IO.Path.Combine(fdoDirs.ProjectsDirectory, database,
				database + FdoFileHelper.ksFwDataXmlFileExtension);
		}

		public IFdoDirectories FdoDirectories { get; private set; }

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
			get { throw new NotImplementedException(); }
		}

		public string Name
		{
			get { return System.IO.Path.GetFileNameWithoutExtension(Path); }
		}

		public FDOBackendProviderType Type { get; private set; }

		public string UiName
		{
			get { return Name; }
		}
		#endregion
	}
}
