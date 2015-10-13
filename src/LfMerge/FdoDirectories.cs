// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public class FdoDirectories: IFdoDirectories
	{
		public FdoDirectories(string baseDir, string releaseDataDir, string templatesDir)
		{
			ProjectsDirectory = Path.IsPathRooted(releaseDataDir) ? releaseDataDir : Path.Combine(baseDir, releaseDataDir);
			TemplateDirectory = Path.IsPathRooted(templatesDir) ? templatesDir : Path.Combine(baseDir, templatesDir);
		}

		#region IFdoDirectories implementation

		public string ProjectsDirectory { get; private set; }

		public string DefaultProjectsDirectory
		{
			get { return ProjectsDirectory; }
		}

		public string TemplateDirectory { get; private set; }

		#endregion
	}
}

