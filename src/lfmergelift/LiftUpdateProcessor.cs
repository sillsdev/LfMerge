using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using System.Windows.Forms;

namespace lfmergelift
{
	class LiftUpdateProcessor
	{
		private HgRepository _hgRepo;
		private LiftUpdatesScanner _liftUpdateScanner;
		private LfSynchronicMerger _lfSynchMerger;
		private LangForgeDirectories lfDirectories;

		/// <summary>
		///
		/// </summary>
		/// <param name="languageForgeServerFolder">This must be the path to the Language Forge folder on the server.</param>
		public LiftUpdateProcessor(String languageForgeServerFolder)
		{
			lfDirectories = new LangForgeDirectories(languageForgeServerFolder);
			_liftUpdateScanner = new LiftUpdatesScanner(lfDirectories.LiftUpdatesPath);
			_lfSynchMerger = new LfSynchronicMerger();
		}

	}

	class LangForgeDirectories
	{
		private String _languageForgeServerFolder;
		//These are the folder names for the location of the repositories for the Language Forge projects
		internal const String WebWorkFolder = "WebWork";
		internal const String MergeWorkFolder = "MergeWork";
		//This is the subfolder of the MergeWork foler where the .lift.update files are found.
		internal const String LiftUpdatesFolder = "LiftUpdates";
		internal const String MergeWorkProjectsFolder = "Projects";

		public LangForgeDirectories(String languageForgeServerFolder)
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
			get { return Path.Combine(MergeWorkPath, MergeWorkProjectsFolder); }
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
	}
}
