// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Chorus.VcsDrivers.Mercurial;
using Palaso.Lift.Merging;

namespace LfMergeLift
{
	/// <summary>
	/// Changes made by Language Forge users will cause .lift.update files to be stored in the /mergeWork/updates folder on the server.
	/// This class provides functions for .lift.update files.
	///
	/// These files will have the following format:    ProjX_sha_timeStamp.lift.update
	/// </summary>
	internal class LiftUpdatesScanner
	{
		private List<UpdateInfo> _updateFilesInfo = new List<UpdateInfo>();
		private string _updatesDirectory;
		private FileInfo[] _liftUpdateFiles;
		private string[] _fileNameParts;
		private List<string> _updateFilesWithWrongNameFormat = new List<string>();

		public LiftUpdatesScanner(string updatesDirectory)
		{
			_updatesDirectory = updatesDirectory;

			CheckForMoreLiftUpdateFiles();
		}

		public void CheckForMoreLiftUpdateFiles()
		{
			_liftUpdateFiles = GetPendingUpdateFiles(_updatesDirectory);

			//????  not sure what I should do if there are no files.
			if (_liftUpdateFiles.Length < 1)
			{
				return;
			}

			GetUpdateInfoForEachLiftUpdateFile();
		}

		public bool ScannerHasListOfLiftUpdates
		{
			get { return _liftUpdateFiles.Length > 0; }
		}


		//Get All Project Names that need updating based on the .lift.update files currently in the liftUpdates folder
		//sorted alphabetically
		public IEnumerable<string> GetProjectsNamesToUpdate()
		{
			var projectNames = new List<string>();
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
		public IEnumerable<string> GetShasForAProjectName(string proj)
		{
			var projectShas = new List<string>();
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

		/// <summary>
		/// Get the .lift.update files for a particular project name and sha. Do not need to sort by timeStamp
		/// since lfSynchronicMerger does that.
		/// </summary>
		/// <param name="proj"></param>
		/// <param name="sha"></param>
		/// <returns></returns>
		public FileInfo[] GetUpdateFilesForProjectAndSha(string proj, string sha)
		{
			var liftUpdateFileGroup = new List<FileInfo>();
			//Get all the fileRecords for this particular Project and Sha
			var updateInfoRecordsForProjectAndSha = from updateInfo in _updateFilesInfo
													where (updateInfo.Project == proj && updateInfo.Sha == sha)
													select updateInfo;
			//Put them in a list of FileInfo so they can be sorted by the last time stamp
			foreach (var fileInfoRecord in updateInfoRecordsForProjectAndSha)
			{
				if (!liftUpdateFileGroup.Contains(fileInfoRecord.SystemFileInfo))
				{
					liftUpdateFileGroup.Add(fileInfoRecord.SystemFileInfo);
				}
			}
			liftUpdateFileGroup.Sort(new FileInfoLastWriteTimeComparer());
			return liftUpdateFileGroup.ToArray();
		}

		private void GetUpdateInfoForEachLiftUpdateFile()
		{
			foreach (var file in _liftUpdateFiles)
			{
				//verify fileName has correct format  ProjX_Sha#_LastPart
				//if not then do something useful with the file
				//if ok then
				//   get project name
				//   get sha part
				//   save full file path
				//   save fileInfo so that can be used to sort the order to process files.
				_fileNameParts = file.Name.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
				if (_fileNameParts.Length < 3)
				{
					// Todo:  We need to remember to add functionality for .lift.update files what have wrong names.
					//fileName does not have at least 3 parts so is in the wrong format. Assumption that last part can also contain underscores so this
					//number can be greater than 3
					//Do something with this file to report an error.
					//Debug.Assert(_fileNameParts.Length >= 3, string.Format("Lift update files should must have the following format ProjX_Sha#_timeStamp: file found with name {0}", fileName));

					if (!_updateFilesWithWrongNameFormat.Contains(file.FullName))
						_updateFilesWithWrongNameFormat.Add(file.FullName);
				}
				else
				{
					var updateFileInfo = new UpdateInfo();
					updateFileInfo.Project = GetProjectName(_fileNameParts);
					updateFileInfo.Sha = GetSha(_fileNameParts);
					updateFileInfo.SystemFileInfo = file;
					_updateFilesInfo.Add(updateFileInfo);
				}
			}
		}



		private static string GetSha(string[] fileNameParts)
		{
			return fileNameParts[1];
		}

		private static string GetProjectName(string[] fileNameParts)
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
