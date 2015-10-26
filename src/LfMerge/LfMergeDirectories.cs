// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using SIL.FieldWorks.FDO;
using LfMerge.Queues;

namespace LfMerge
{
	public class LfMergeDirectories: IFdoDirectories
	{
		private string[] _queueDirectories;

		private LfMergeDirectories(string baseDir, string releaseDataDir, string templatesDir)
		{
			ProjectsDirectory = Path.IsPathRooted(releaseDataDir) ? releaseDataDir : Path.Combine(baseDir, releaseDataDir);
			TemplateDirectory = Path.IsPathRooted(templatesDir) ? templatesDir : Path.Combine(baseDir, templatesDir);
			StateDirectory = Path.Combine(baseDir, "state");

			var queueCount = Enum.GetValues(typeof(QueueNames)).Length;
			_queueDirectories = new string[queueCount];
			_queueDirectories[(int)QueueNames.None] = null;
			_queueDirectories[(int)QueueNames.Merge] = Path.Combine(baseDir, "mergequeue");
			_queueDirectories[(int)QueueNames.Commit] = Path.Combine(baseDir, "commitqueue");
			_queueDirectories[(int)QueueNames.Receive] = Path.Combine(baseDir, "receivequeue");
			_queueDirectories[(int)QueueNames.Send] = Path.Combine(baseDir, "sendqueue");

			ConfigFile = "/etc/languageforge/conf/sendreceive.conf";
		}

		public static void Initialize(string baseDir, string releaseDataDir = "ReleaseData",
			string templatesDir = "Templates")
		{
			Current = new LfMergeDirectories(baseDir, releaseDataDir, templatesDir);

			Queue.CreateQueueDirectories();
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

		public string GetQueueDirectory(QueueNames queue)
		{
			return _queueDirectories[(int)queue];
		}

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

