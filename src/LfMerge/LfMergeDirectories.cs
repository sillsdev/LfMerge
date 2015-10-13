// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public class LfMergeDirectories: IFdoDirectories
	{
		public LfMergeDirectories(string baseDir, string releaseDataDir = "ReleaseData",
			string templatesDir = "Templates")
		{
			ProjectsDirectory = Path.IsPathRooted(releaseDataDir) ? releaseDataDir : Path.Combine(baseDir, releaseDataDir);
			TemplateDirectory = Path.IsPathRooted(templatesDir) ? templatesDir : Path.Combine(baseDir, templatesDir);
			StateDirectory = Path.Combine(baseDir, "state");
			MergeQueueDirectory = Path.Combine(baseDir, "mergequeue");
			CommitQueueDirectory = Path.Combine(baseDir, "commitqueue");
			ReceiveQueueDirectory = Path.Combine(baseDir, "receivequeue");
			SendQueueDirectory = Path.Combine(baseDir, "sendqueue");

			ConfigFile = "/etc/languageforge/conf/sendreceive.conf";

			Current = this;
		}

		public static LfMergeDirectories Current { get; private set; }

		#region IFdoDirectories implementation

		public string ProjectsDirectory { get; private set; }

		public string DefaultProjectsDirectory
		{
			get { return ProjectsDirectory; }
		}

		public string TemplateDirectory { get; private set; }

		#endregion

		public string StateDirectory { get; private set; }

		public string MergeQueueDirectory { get; private set; }

		public string CommitQueueDirectory { get; private set; }

		public string ReceiveQueueDirectory { get; private set; }

		public string SendQueueDirectory { get; private set; }

		public string ConfigFile { get; private set; }

		public string WebWorkDirectory { get { return ProjectsDirectory; } }

		/// <summary>
		/// Gets the name of the state file. If necessary the state directory is also created.
		/// </summary>
		/// <returns>The state file name.</returns>
		/// <param name="projectCode">Project code.</param>
		public string GetStateFileName(string projectCode)
		{
			Directory.CreateDirectory(StateDirectory);
			return Path.Combine(StateDirectory, projectCode + ".state");
		}
	}
}

