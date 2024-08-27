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
		// Settings derived from environment variables

		public string BaseDir => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_BaseDir) ?? DefaultLfMergeSettings.BaseDir;

		public string WebworkDir {
			get {
				string _webworkDir = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_WebworkDir) ?? DefaultLfMergeSettings.WebworkDir;
				_webworkDir = Path.IsPathRooted(_webworkDir) ? _webworkDir : Path.Combine(BaseDir, _webworkDir);
				return _webworkDir;
			}
		}

		public string TemplatesDir {
			get {
				string _templatesDir = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_TemplatesDir) ?? DefaultLfMergeSettings.TemplatesDir;
				_templatesDir = Path.IsPathRooted(_templatesDir) ? _templatesDir : Path.Combine(BaseDir, _templatesDir);
				return _templatesDir;
			}
		}

		private string _mongoHostname;
		public string MongoHostname {
			get => _mongoHostname ?? Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoHostname) ?? DefaultLfMergeSettings.MongoHostname;
			set => _mongoHostname = value;
		}

		private string _mongoPort;
		public string MongoPort {
			get => _mongoPort ?? Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoPort) ?? DefaultLfMergeSettings.MongoPort.ToString();
			set => _mongoPort = value;
		}

		public string MongoAuthSource => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoAuthSource) ?? DefaultLfMergeSettings.MongoAuthSource;
		public string MongoUsername => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoUsername) ?? DefaultLfMergeSettings.MongoUsername;
		public string MongoPassword => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoPassword) ?? DefaultLfMergeSettings.MongoPassword;
		private string EncodedUsername => Uri.EscapeUriString(MongoUsername);
		private string EncodedPassword => Uri.EscapeUriString(MongoPassword);

		public string MongoMainDatabaseName => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoMainDatabaseName) ?? DefaultLfMergeSettings.MongoMainDatabaseName;

		/// <summary>
		/// The prefix prepended to project codes to get the Mongo database name.
		/// </summary>
		public string MongoDatabaseNamePrefix => Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_MongoDatabaseNamePrefix) ?? DefaultLfMergeSettings.MongoDatabaseNamePrefix;

		public bool VerboseProgress {
			get {
				string _verboseProgressStr = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_VerboseProgress) ?? "";
				return LanguageForge.Model.ParseBoolean.FromString(_verboseProgressStr);
			}
		}

		private string _languageDepotRepoUri;
		public string LanguageDepotRepoUri {
			get => _languageDepotRepoUri ?? Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri) ?? DefaultLfMergeSettings.LanguageDepotRepoUri;
			set => _languageDepotRepoUri = value;
		}

		// Settings calculated at runtime from sources other than environment variables

		public bool CommitWhenDone { get; internal set; }

		public string MongoDbHostNameAndPort => $"{MongoHostname}:{MongoPort}";
		public string MongoDbHostPortAndAuth => string.IsNullOrEmpty(MongoUsername) || string.IsNullOrEmpty(MongoPassword)
			? string.Format("{0}:{1}", MongoHostname, MongoPort)
			: string.Format("{0}:{1}@{2}:{3}", EncodedUsername, EncodedPassword, MongoHostname, MongoPort);

		private string QueueDirectory { get; set; }

		public string StateDirectory { get; private set; }

		public string WebWorkDirectory { get { return LcmDirectorySettings.ProjectsDirectory; } }

		public LcmDirectories LcmDirectorySettings { get; private set; }

		public LfMergeSettings()
		{
			LcmDirectorySettings = new LcmDirectories();
			SetAllMembers();
		}

		public void Initialize()
		{
			// TODO: Get rid of this once we simplify the queue system. 2022-02 RM
			SetAllMembers();
		}

		private void SetAllMembers()
		{
			LcmDirectorySettings.SetProjectsDirectory(WebworkDir);
			LcmDirectorySettings.SetTemplateDirectory(TemplatesDir);
			StateDirectory = Path.Combine(BaseDir, "state");
			QueueDirectory = Path.Combine(BaseDir, "syncqueue");
			Directory.CreateDirectory(QueueDirectory);

			CommitWhenDone = true;
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
				var path = "/var/run/lfmerge";
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

		public string GetQueueDirectory()
		{
			return QueueDirectory;
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

