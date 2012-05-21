using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using Palaso.Lift.Merging;

namespace lfmergelift
{
	/// <summary>
	/// Changes made by Language Forge users will cause .lift.update files to be stored in the /mergeWork/updates folder on the server.
	/// This class provides functions for .lift.update files.
	///
	/// These files will have the following format:    ProjX_sha_timeStamp.lift.update
	/// </summary>
	internal class LiftUpdatesScanner
	{
		private HgRepository _hgRepo;
		private List<UpdateInfo> _updateFilesInfo =new List<UpdateInfo>();
		private String _updatesDirectory;
		private FileInfo[] _liftUpdateFiles;
		private String[] _fileNameParts;
		private List<String> _updateFilesWithWrongNameFormat = new List<String>();

		public LiftUpdatesScanner(String updatesDirectory)
		{
			_updatesDirectory = updatesDirectory;
			_liftUpdateFiles = GetPendingUpdateFiles(_updatesDirectory);

			//????  not sure what I should do if there are no files.
			if (_liftUpdateFiles.Length < 1)
			{
				return;
			}

			//We will need to sort files by time stamp but maybe not here
			//Array.Sort(_liftUpdateFiles, new FileInfoLastWriteTimeComparer());
			int count = _liftUpdateFiles.Length;
			GetUpdateInfoForEachLiftUpdateFile();
		}


		//Get All Project Names that need updating based on the .lift.update files currently in the liftUpdates folder
		//sorted alphabetically
		public IEnumerable<String> GetProjectsNamesToUpdate()
		{
			var projectNames = new List<String>();
			foreach (var fileInfoRecord in _updateFilesInfo)
			{
				if (!projectNames.Contains(fileInfoRecord.Project))
				{
					projectNames.Add(fileInfoRecord.Project);
				}
			}
			projectNames.Sort();
			return projectNames;
		}

		//Get All Sha's for a particular Project based on the .lift.update files currently in the liftUpdates folder
		public IEnumerable<String> GetShasForAProjectName(String proj)
		{
			var projectShas = new List<String>();
			var updateInfoRecordsForProject = from updateInfo in _updateFilesInfo
									where updateInfo.Project == proj
									select updateInfo;
			foreach (var fileInfoRecord in updateInfoRecordsForProject)
			{
				if (!projectShas.Contains(fileInfoRecord.Sha))
				{
					projectShas.Add(fileInfoRecord.Sha);
				}
			}
			projectShas.Sort();
			return projectShas;
		}

		//Get the .lift.update files for a particular project name and sha. Sort by file timeStamp


		private void GetUpdateInfoForEachLiftUpdateFile()
		{
			foreach (FileInfo file in _liftUpdateFiles)
			{
				var filePath = file.FullName;
				var fileName = file.Name;
				//verify fileName has correct format  ProjX_Sha#_LastPart
				//if not then do something useful with the file
				//if ok then
				//   get project name
				//   get sha part
				//   save full file path
				//   save fileInfo so that can be used to sort the order to process files.
				_fileNameParts = fileName.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
				if (_fileNameParts.Length < 3)
				{
					//fileName does not have at least 3 parts so is in the wrong format. Assumption that last part can also contain underscores so this
					//number can be greater than 3
					//Do something with this file to report an error.
					//Debug.Assert(_fileNameParts.Length >= 3, String.Format("Lift update files should must have the following format ProjX_Sha#_timeStamp: file found with name {0}", fileName));

					if (!_updateFilesWithWrongNameFormat.Contains(file.FullName))
						_updateFilesWithWrongNameFormat.Add(file.FullName);
				}
				else
				{
					var updateFileInfo = new UpdateInfo();
					updateFileInfo.Project = GetProjectName(_fileNameParts);
					updateFileInfo.Sha = GetSha(_fileNameParts);
					updateFileInfo.UpdateFilePath = filePath;
					updateFileInfo.SystemFileInfo = file;
					_updateFilesInfo.Add(updateFileInfo);
				}
			}
		}

		private static string GetSha(String[] fileNameParts)
		{
			return fileNameParts[1];
		}

		private static string GetProjectName(String[] fileNameParts)
		{
			return fileNameParts[0];
		}

		public FileInfo[] LiftUpdateFiles
		{
			get { return _liftUpdateFiles; }
		}

		public IEnumerable<UpdateInfo> LiftUpdateFilesInfo
		{
			get { return _updateFilesInfo; }
		}

		public List<string> UpdateFilesWithWrongNameFormat
		{
			get { return _updateFilesWithWrongNameFormat; }
		}

		public static FileInfo[] GetPendingUpdateFiles(string pathToLiftUpdates)
		{
			if (!pathToLiftUpdates.Contains(Path.DirectorySeparatorChar.ToString()))
			{
				throw new ArgumentException("pathToLiftUpdates must be a full path. Path was " + pathToLiftUpdates);
			}
			// ReSharper disable AssignNullToNotNullAttribute
			var di = new DirectoryInfo(pathToLiftUpdates);
			// ReSharper restore AssignNullToNotNullAttribute
			return di.GetFiles("*" + SynchronicMerger.ExtensionOfIncrementalFiles, SearchOption.TopDirectoryOnly);
		}



	}

	class UpdateInfo
	{
		public string Project { get; set; }
		public string Sha { get; set; }
		public string UpdateFilePath { get; set; }
		public FileInfo SystemFileInfo { get; set; }
	}

	internal class FileInfoLastWriteTimeComparer : IComparer<FileInfo>
	{
		public int Compare(FileInfo x, FileInfo y)
		{
			int timecomparison = DateTime.Compare(x.LastWriteTimeUtc, y.LastWriteTimeUtc);
			if (timecomparison == 0)
			{
				// if timestamps are the same, then sort by name
				return StringComparer.OrdinalIgnoreCase.Compare(x.FullName, y.FullName);
			}
			return timecomparison;
		}
	}
}
