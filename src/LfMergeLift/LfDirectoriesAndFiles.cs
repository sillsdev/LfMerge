// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;

namespace LfMergeLift
{
	class LfDirectoriesAndFiles
	{
		public static LfDirectoriesAndFiles Current { get; private set; }

		private readonly string _languageForgeServerFolder;

		//These are the folder names for the location of the repositories for the Language Forge projects
		internal const string WebWorkFolder = "WebWork";
		internal const string MergeWorkFolder = "MergeWork";
		internal const string MasterReposFolder = "MasterRepos";
		//This is the subfolder of the MergeWork foler where the .lift.update files are found.
		internal const string LiftUpdatesFolder = "LiftUpdates";
		internal const string ProjectsFolder = "Projects";
		internal const string StateFolder = "state";

		public LfDirectoriesAndFiles(string languageForgeServerFolder)
		{
			if (!languageForgeServerFolder.Contains(Path.DirectorySeparatorChar.ToString()))
			{
				throw new ArgumentException("languageForgeServerFolder must be a full path, not just a file name. Path was " + languageForgeServerFolder);
			}
			_languageForgeServerFolder = languageForgeServerFolder;

			Current = this;
		}

		/// <summary>
		/// This folder contains a subfolder for each language project's repository and LIFT that
		/// the Language Forge clients are reading from.
		/// e.g .../LangForge/WebWork/ProjA     .../LangForge/WebWork/ProjB
		/// </summary>
		public string WebWorkPath
		{
			get { return Path.Combine(_languageForgeServerFolder, WebWorkFolder); }
		}

		/// <summary>
		/// This folder contains a subfolder for each language project's repository and LIFT file.
		/// e.g .../LangForge/MasterRepos/ProjA     .../LangForge/MasterRepos/ProjB
		/// </summary>
		public string MasterReposPath
		{
			get { return Path.Combine(_languageForgeServerFolder, MasterReposFolder); }
		}

		/// <summary>
		/// This folder contains the .state files
		/// </summary>
		public string StatePath
		{
			get { return Path.Combine(_languageForgeServerFolder, StateFolder); }
		}


		/// <summary>
		/// This is the location where the updates from language forge clients are done
		/// e.g.  .../LangForge/MergeWork/
		/// </summary>
		public string MergeWorkPath
		{
			get { return Path.Combine(_languageForgeServerFolder, MergeWorkFolder); }
		}

		/// <summary>
		/// This folder contains one folder for each language project the server managing for Language Forge
		/// clients.
		/// e.g. .../LangForge/MergeWork/Projects/ProjA     .../LangForge/MergeWork/Projects/ProjB
		/// </summary>
		public string MergeWorkProjects
		{
			get { return Path.Combine(MergeWorkPath, ProjectsFolder); }
		}

		/// <summary>
		/// This folder contains the .lift.update files that are produced by the Language Forge clients and need to be applied
		/// to the appropriate projects under the MergeWorkProjects folder.
		/// e.g.  .../LangForge/MergeWork/LiftUpdates
		/// </summary>
		public string LiftUpdatesPath
		{
			get { return Path.Combine(MergeWorkPath, LiftUpdatesFolder); }
		}

		public void CreateWebWorkFolder()
		{
			Directory.CreateDirectory(WebWorkPath);
		}

		public void CreateMasterReposFolder()
		{
			Directory.CreateDirectory(MasterReposPath);
		}

		public string CreateMasterReposProjectFolder(string projectName)
		{
			var dir = GetProjMasterRepoPath(projectName);
			Directory.CreateDirectory(dir);
			return dir;
		}

		public string CreateWebWorkProjectFolder(string projectName)
		{
			var dir = GetProjWebPath(projectName);
			Directory.CreateDirectory(dir);
			return dir;
		}

		public string CreateStateFolder()
		{
			var dir = StatePath;
			Directory.CreateDirectory(dir);
			return dir;
		}

		public string CreateMergeWorkProjectFolder(string projectName)
		{
			var dir = GetProjMergePath(projectName);
			Directory.CreateDirectory(dir);
			return dir;
		}

		public void CreateMergeWorkFolder()
		{
			Directory.CreateDirectory(MergeWorkPath);
		}

		public void CreateLiftUpdatesFolder()
		{
			Directory.CreateDirectory(LiftUpdatesPath);
		}

		public void CreateMergeWorkProjectsFolder()
		{
			Directory.CreateDirectory(MergeWorkProjects);
		}

		public string GetProjWebPath(string projectName)
		{
			return Path.Combine(WebWorkPath, projectName);
		}

		public string GetProjMasterRepoPath(string projectName)
		{
			return Path.Combine(MasterReposPath, projectName);
		}

		public string GetProjMergePath(string projectName)
		{
			return Path.Combine(MergeWorkProjects, projectName);
		}

		public const string ExtensionOfLiftFiles = ".lift";
		public const string ExtensionOfStateFiles = ".state";

		public string LiftFileMergePath(string projName)
		{
			return Path.Combine(GetProjMergePath(projName), projName + ExtensionOfLiftFiles);
		}

		public string LiftFileWebWorkPath(string projName)
		{
			return Path.Combine(GetProjWebPath(projName), projName + ExtensionOfLiftFiles);
		}

		public string LiftFileMasterRepoPath(string projName)
		{
			return Path.Combine(GetProjMasterRepoPath(projName), projName + ExtensionOfLiftFiles);
		}

		public string LfMergeStateFile(string projName)
		{
			return Path.Combine(StatePath, projName + ExtensionOfStateFiles);
		}
	}
}