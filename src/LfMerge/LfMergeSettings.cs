// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using SIL.FieldWorks.FDO;
using LfMerge.Queues;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace LfMerge
{
	public class LfMergeSettings: IFdoDirectories
	{
		public static LfMergeSettings Current { get; protected set; }

		[JsonProperty]
		public static string ConfigDir { get; set; }
		public static string ConfigFile
		{
			get { return Path.Combine(ConfigDir, "sendreceive.conf"); }
		}

		static LfMergeSettings()
		{
			ConfigDir = "/etc/languageforge/conf/";
		}

		public static void Initialize(string baseDir = null, string releaseDataDir = "ReleaseData",
			string templatesDir = "Templates")
		{
			if (Current != null)
				return;

			if (string.IsNullOrEmpty(baseDir))
			{
				baseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			}
			Current = new LfMergeSettings(baseDir, releaseDataDir, templatesDir);

			Queue.CreateQueueDirectories();
		}

		[JsonProperty]
		private string[] QueueDirectories { get; set; }

		protected LfMergeSettings()
		{
		}

		private LfMergeSettings(string baseDir, string releaseDataDir, string templatesDir)
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

			MongoDbHostNameAndPort = "localhost:27017";
		}

		#region Equality and GetHashCode
		public override bool Equals(object obj)
		{
			var other = obj as LfMergeSettings;
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

		public static string LockFile
		{
			get
			{
				var path = "/var/run";
				const string filename = "lfmerge.pid";

				var attributes = File.GetAttributes(path);
				if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					// XDG_RUNTIME_DIR is /run/user/<userid>, and /var/run is symlink'ed to /run
					path = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
				}
				return Path.Combine(path, filename);
			}
		}

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

		// see http://stackoverflow.com/a/31617183
		public class NonPublicPropertiesResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);
				var propertyInfo = member as PropertyInfo;
				if (propertyInfo != null)
				{
					property.Readable = (propertyInfo.GetMethod != null);
					property.Writable = (propertyInfo.SetMethod != null);
				}
				return property;
			}
		}

		// we don't call this method from our production code since LfMerge doesn't directly
		// change the option.
		public void SaveSettings()
		{
			JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
				ContractResolver = new NonPublicPropertiesResolver()
			};
			var json = JsonConvert.SerializeObject(this);

			File.WriteAllText(ConfigFile, json);
		}

		public static LfMergeSettings LoadSettings()
		{
			var fileName = ConfigFile;
			LfMergeSettings.Current = null;
			string json = File.Exists(fileName) ? File.ReadAllText(fileName) : "";
			if (!String.IsNullOrWhiteSpace(json))
			{
				JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
					ContractResolver = new NonPublicPropertiesResolver()
				};
				LfMergeSettings.Current = JsonConvert.DeserializeObject<LfMergeSettings>(json);
				return LfMergeSettings.Current;
			}
			LfMergeSettings.Initialize();
			return LfMergeSettings.Current;
		}
		#endregion

	}
}

