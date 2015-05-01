// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;

namespace LfMergeLift
{
	class LfDirectoriesAndFiles
	{
		private String _languageForgeServerFolder;
		//These are the folder names for the location of the repositories for the Language Forge projects
		internal const String WebWorkFolder = "WebWork";
		internal const String MergeWorkFolder = "MergeWork";
		internal const String MasterReposFolder = "MasterRepos";
		//This is the subfolder of the MergeWork foler where the .lift.update files are found.
		internal const String LiftUpdatesFolder = "LiftUpdates";
		internal const String ProjectsFolder = "Projects";

		public LfDirectoriesAndFiles(String languageForgeServerFolder)
		{
			if (!languageForgeServerFolder.Contains(Path.DirectorySeparatorChar.ToString()))
			{
				throw new ArgumentException("languageForgeServerFolder must be a full path, not just a file name. Path was " + languageForgeServerFolder);
			}
			_languageForgeServerFolder = languageForgeServerFolder;

		}

		/// <summary>
		/// This folder contains a subfolder for each language project's repository and LIFT that the the Language Forge
		/// clients are reading from.
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

		public String CreateMasterReposProjectFolder(String projectName)
		{
			Directory.CreateDirectory(GetProjMasterRepoPath(projectName));
			return GetProjMasterRepoPath(projectName);
		}

		public String CreateWebWorkProjectFolder(String projectName)
		{
			Directory.CreateDirectory(GetProjWebPath(projectName));
			return GetProjWebPath(projectName);
		}

		public String CreateMergeWorkProjectFolder(String projectName)
		{
			Directory.CreateDirectory(GetProjMergePath(projectName));
			return GetProjMergePath(projectName);
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

		public String GetProjWebPath(String projectName)
		{
			return Path.Combine(WebWorkPath, projectName);
		}

		public String GetProjMasterRepoPath(String projectName)
		{
			return Path.Combine(MasterReposPath, projectName);
		}

		public String GetProjMergePath(String projectName)
		{
			return Path.Combine(MergeWorkProjects, projectName);
		}

		public const string ExtensionOfLiftFiles = ".lift";
		public string LiftFileMergePath(String projName)
		{
			return Path.Combine(GetProjMergePath(projName), projName + ExtensionOfLiftFiles);
		}

		public string LiftFileWebWorkPath(String projName)
		{
			return Path.Combine(GetProjWebPath(projName), projName + ExtensionOfLiftFiles);
		}

		public string LiftFileMasterRepoPath(String projName)
		{
			return Path.Combine(GetProjMasterRepoPath(projName), projName + ExtensionOfLiftFiles);
		}
	}
}