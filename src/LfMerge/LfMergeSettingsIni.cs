// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using System.Text;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using IniParser.Exceptions;
using LfMerge.Queues;

namespace LfMerge
{
	public class LfMergeSettingsIni : ILfMergeSettings
	{
		public static LfMergeSettingsIni Current { get; protected set; }

		public static string ConfigDir { get; set; }
		public static string UserConfigDir { get; set; }
		public static string ConfigFile
		{
			//get { return Path.Combine(ConfigDir, "sendreceive.conf"); }
			get { return Path.Combine(ConfigDir, "sendreceive.ini"); } // Only use .ini during testing, then revert to .conf
		}
		public static string UserConfigFile
		{
			//get { return Path.Combine(UserConfigDir, "sendreceive.conf"); }
			get { return Path.Combine(UserConfigDir, "sendreceive.ini"); }
		}

		static LfMergeSettingsIni()
		{
			ConfigDir = "/etc/languageforge/conf/";
			UserConfigDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".config", "languageforge");
		}

		protected IniData ParsedConfig { get; set; }

		public void Initialize(IniData parsedConfig)
		{
			Console.WriteLine("LfMergeSettingsIni.Initialize() was called with config: {0}", parsedConfig);
//			if (Current != null)
//				return;

			KeyDataCollection main = parsedConfig.Global ?? new KeyDataCollection();
			string baseDir = main["BaseDir"] ?? Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			string releaseDataDir = main["ReleaseDataDir"] ?? "ReleaseData";
			string templatesDir = main["TemplatesDir"] ?? "Templates";
			string mongoHostname = main["MongoHostname"] ?? "localhost";
			string mongoPort = main["MongoPort"] ?? "27017";

			SetAllMembers(baseDir, releaseDataDir, templatesDir, mongoHostname, mongoPort);

			Queue.CreateQueueDirectories(this); // TODO: I believe this call properly belongs elsewhere.
		}

		private string[] QueueDirectories { get; set; }

		public LfMergeSettingsIni()
		{
			// Save parsed config for easier persisting in SaveSettings()
			ParsedConfig = ParseFiles(DefaultLfMergeSettings.DefaultIniText, ConfigFile, UserConfigFile);
			Initialize(ParsedConfig);
		}

		private void SetAllMembers(string baseDir, string releaseDataDir, string templatesDir, string mongoHostname, string mongoPort)
		{
			ProjectsDirectory = Path.IsPathRooted(releaseDataDir) ? releaseDataDir : Path.Combine(baseDir, releaseDataDir);
			TemplateDirectory = Path.IsPathRooted(templatesDir) ? templatesDir : Path.Combine(baseDir, templatesDir);
			StateDirectory = Path.Combine(baseDir, "state");

			var queueCount = Enum.GetValues(typeof(QueueNames)).Length;
			QueueDirectories = new string[queueCount];
			QueueDirectories[(int)QueueNames.None] = null;
			QueueDirectories[(int)QueueNames.Merge] = Path.Combine(baseDir, "mergequeue");
			QueueDirectories[(int)QueueNames.Commit] = Path.Combine(baseDir, "commitqueue");
			QueueDirectories[(int)QueueNames.Receive] = Path.Combine(baseDir, "receivequeue");
			QueueDirectories[(int)QueueNames.Send] = Path.Combine(baseDir, "sendqueue");

			MongoDbHostNameAndPort = String.Format("{0}:{1}", mongoHostname, mongoPort);
		}

		#region Equality and GetHashCode
		public override bool Equals(object obj)
		{
			var other = obj as LfMergeSettingsIni;
			if (other == null)
				return false;
			bool ret =
				other.DefaultProjectsDirectory == DefaultProjectsDirectory &&
				other.MongoDbHostNameAndPort == MongoDbHostNameAndPort &&
				other.ProjectsDirectory == ProjectsDirectory &&
				other.StateDirectory == StateDirectory &&
				other.TemplateDirectory == TemplateDirectory &&
				other.WebWorkDirectory == WebWorkDirectory;
			foreach (QueueNames queueName in Enum.GetValues(typeof(QueueNames)))
			{
				ret = ret && other.GetQueueDirectory(queueName) == GetQueueDirectory(queueName);
			}
			return ret;
		}

		public override int GetHashCode()
		{
			var hash = DefaultProjectsDirectory.GetHashCode() ^
				MongoDbHostNameAndPort.GetHashCode() ^ ProjectsDirectory.GetHashCode() ^
				StateDirectory.GetHashCode() ^ TemplateDirectory.GetHashCode() ^
				WebWorkDirectory.GetHashCode();
			foreach (QueueNames queueName in Enum.GetValues(typeof(QueueNames)))
			{
				var dir = GetQueueDirectory(queueName);
				if (dir != null)
					hash ^= dir.GetHashCode();
			}
			return hash;
		}
		#endregion

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
			return QueueDirectories[(int)queue];
		}

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

		public string MongoDbHostNameAndPort { get; private set; }

		#region Serialization/Deserialization

		// we don't call this method from our production code since LfMerge doesn't directly
		// change the option.
		public void SaveSettings()
		{
			SaveSettings(ConfigFile);
		}

		public void SaveSettings(string fileName)
		{
			if (ParsedConfig == null)
				ParsedConfig = new IniData();
			// Note that this will persist the merged global & user configurations, not the original global config.
			// Since we don't call this method from production code, this is not an issue.
			// TODO: parserConfig is also created in ParseFiles. Consider making it a static member for consistency.
			var parserConfig = new IniParserConfiguration {
				// ThrowExceptionsOnError = false,
				CommentString = "#",
				SkipInvalidLines = true
			};
			var parser = new IniDataParser(parserConfig);
			var fileParser = new FileIniDataParser(parser);
			var utf8 = new UTF8Encoding(false);
			fileParser.WriteFile(fileName, ParsedConfig, utf8);
		}

		public static IniData ParseFiles(string defaultConfig, string globalConfigFilename, string userConfigFilename)
		{
			var utf8 = new UTF8Encoding(false);
			var parserConfig = new IniParserConfiguration {
				// ThrowExceptionsOnError = false,
				CommentString = "#",
				SkipInvalidLines = true
			};

			var parser = new IniDataParser(parserConfig);
			IniData result = parser.Parse(DefaultLfMergeSettings.DefaultIniText);

			string globalIni = File.Exists(globalConfigFilename) ? File.ReadAllText(globalConfigFilename, utf8) : "";
			if (String.IsNullOrEmpty(globalIni)) {
				// TODO: Make all these Console.WriteLine calls into proper logging calls
				Console.WriteLine("Warning: no global configuration found. Will use default settings.");
			}
			IniData globalConfig;
			try
			{
				globalConfig = parser.Parse(globalIni);
			}
			catch (ParsingException e) {
				Console.WriteLine("Warning: Error parsing global configuration file. Will use default settings.");
				Console.WriteLine("Error follows: {0}", e.ToString());
				globalConfig = null; // Merging null is perfectly acceptable to IniParser
			}
			result.Merge(globalConfig);

			string userIni = File.Exists(userConfigFilename) ? File.ReadAllText(userConfigFilename, utf8) : "";
			// No warning needed if user config doesn't exist.
			IniData userConfig;
			try
			{
				userConfig = parser.Parse(userIni);
			}
			catch (ParsingException e) {
				Console.WriteLine("Warning: Error parsing user configuration file. Will use global settings.");
				Console.WriteLine("Error follows: {0}", e.ToString());
				userConfig = null; // Merging null is perfectly acceptable to IniParser
			}
			result.Merge(userConfig);

			foreach (KeyData item in result.Global) {
				// Special-case. Could be replaced with a more general regex if we end up using more variables, but YAGNI.
				if (item.Value.Contains("${HOME}")) {
					item.Value = item.Value.Replace("${HOME}", Environment.GetEnvironmentVariable("HOME"));
				}
			}

			return result;
		}

		#endregion

	}
}

