// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Text;
using LfMerge.Core.Queues;
using SIL.LCModel;

namespace LfMerge.Core.Settings
{
	public class LfMergeSettings
	{
		private string _baseDir = null;
		private string _webworkDir = null;
		private string _templatesDir = null;
		private string _mongoHostname = null;
		private int _mongoPort = 0;
		private string _mongoMainDatabaseName = null;
		private string _mongoDatabaseNamePrefix = null;
		private string _verboseProgressStr = null;
		private bool _verboseProgress = false;
		private string _languageDepotRepoUri = null;

		// Settings derived from environment variables

		public string BaseDir {
			get {
				if (_baseDir != null) return _baseDir;
				else {
					_baseDir = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_BaseDir) ?? DefaultLfMergeSettings.BaseDir;
					return _baseDir;
				}
			}
		}

		public string WebworkDir {
			get {
				if (_webworkDir != null) return _webworkDir;
				else {
					_webworkDir = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_WebworkDir) ?? DefaultLfMergeSettings.WebworkDir;
					_webworkDir = Path.IsPathRooted(_webworkDir) ? _webworkDir : Path.Combine(BaseDir, _webworkDir);
					return _webworkDir;
				}
			}
		}

		public string TemplatesDir {
			get {
				if (_templatesDir != null) return _templatesDir;
				else {
					_templatesDir = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_TemplatesDir) ?? DefaultLfMergeSettings.TemplatesDir;
					_templatesDir = Path.IsPathRooted(_templatesDir) ? _templatesDir : Path.Combine(BaseDir, _templatesDir);
					return _templatesDir;
				}
			}
		}

		public string MongoHostname {
			get {
				if (_mongoHostname != null) return _mongoHostname;
				else {
					_mongoHostname = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoHostname) ?? DefaultLfMergeSettings.MongoHostname;
					return _mongoHostname;
				}
			}
		}

		public int MongoPort {
			get {
				if (_mongoPort != 0) return _mongoPort;
				else {
					string mongoPortStr = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoPort);
					if (mongoPortStr == null) mongoPortStr = "";
					if (Int32.TryParse(mongoPortStr, out _mongoPort)) {
						return _mongoPort;
					} else {
						_mongoPort = DefaultLfMergeSettings.MongoPort;
						return _mongoPort;
					}
				}
			}
		}

		public string MongoMainDatabaseName {
			get {
				if (_mongoMainDatabaseName != null) return _mongoMainDatabaseName;
				else {
					_mongoMainDatabaseName = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoMainDatabaseName) ?? DefaultLfMergeSettings.MongoMainDatabaseName;
					return _mongoMainDatabaseName;
				}
			}
		}

		/// <summary>
		/// The prefix prepended to project codes to get the Mongo database name.
		/// </summary>
		public string MongoDatabaseNamePrefix {
			get {
				if (_mongoDatabaseNamePrefix != null) return _mongoDatabaseNamePrefix;
				else {
					_mongoDatabaseNamePrefix = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoDatabaseNamePrefix) ?? DefaultLfMergeSettings.MongoDatabaseNamePrefix;
					return _mongoDatabaseNamePrefix;
				}
			}
		}

		public bool VerboseProgress {
			get {
				if (_verboseProgressStr != null) return _verboseProgress;
				else {
					_verboseProgressStr = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_VerboseProgress);
					if (_verboseProgressStr == null) _verboseProgressStr = "";
					_verboseProgress = LanguageForge.Model.ParseBoolean.FromString(_verboseProgressStr);
					return _verboseProgress;
				}
			}
		}

		public string LanguageDepotRepoUri {
			get {
				if (_languageDepotRepoUri != null) return _languageDepotRepoUri;
				else {
					_languageDepotRepoUri = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri) ?? DefaultLfMergeSettings.LanguageDepotRepoUri;
					return _languageDepotRepoUri;
				}
			}
		}

		// Settings calculated at runtime from sources other than environment variables

		public bool CommitWhenDone { get; internal set; }

		public string MongoDbHostNameAndPort { get { return String.Format("{0}:{1}", MongoHostname, MongoPort.ToString()); } }

		private string[] QueueDirectories { get; set; }

		public string StateDirectory { get; private set; }

		public string WebWorkDirectory { get { return LcmDirectorySettings.ProjectsDirectory; } }

		public LcmDirectories LcmDirectorySettings { get; private set; }

		public LfMergeSettings()
		{
			LcmDirectorySettings = new LcmDirectories();
			QueueDirectories = new string[0];
		}

		public void Initialize()
		{
			SetAllMembers();

			// TODO: Get rid of this once we simplify the queue system. 2022-02 RM
			Queue.CreateQueueDirectories(this);
		}

		private void SetAllMembers()
		{
			LcmDirectorySettings.SetProjectsDirectory(WebworkDir);
			LcmDirectorySettings.SetTemplateDirectory(TemplatesDir);
			StateDirectory = Path.Combine(BaseDir, "state");

			CommitWhenDone = true;

			var queueCount = Enum.GetValues(typeof(QueueNames)).Length;
			QueueDirectories = new string[queueCount];
			QueueDirectories[(int)QueueNames.None] = null;
			QueueDirectories[(int)QueueNames.Edit] = Path.Combine(BaseDir, "editqueue");
			QueueDirectories[(int)QueueNames.Synchronize] = Path.Combine(BaseDir, "syncqueue");
		}

		public class LcmDirectories: ILcmDirectories
		{
			public void SetProjectsDirectory(string value)
			{
				ProjectsDirectory = value;
			}

			public void SetTemplateDirectory(string value)
			{
				TemplateDirectory = value;
			}

			#region ILcmDirectories implementation

			public string ProjectsDirectory { get; private set; }

			// TODO: What is the point of this? Determine if we can get rid of it. 2022-02 RM
			public string DefaultProjectsDirectory {
				get { return ProjectsDirectory; }
			}

			public string TemplateDirectory { get; private set; }

			#endregion
		}

		public string LockFile
		{
			get
			{
				var path = "/var/run";
				const string filename = "lfmerge.pid";

				var attributes = File.GetAttributes(path);
				if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					// XDG_RUNTIME_DIR is /run/user/<userid>, and /var/run is symlink'ed to /run. See http://serverfault.com/a/727994/246397
					path = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/tmp/run";
					if (!Directory.Exists(path)) {
						Directory.CreateDirectory(path);
					}
				}

				return Path.Combine(path, filename);
			}
		}

		public string GetQueueDirectory(QueueNames queue)
		{
			return QueueDirectories[(int)queue];
		}

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

