// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IniParser.Model;
using LfMerge.Core.Actions;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Moq;

namespace LfMerge.Core.Tests
{
	public class ProcessingStateFactoryDouble: IProcessingStateDeserialize
	{
		public ProcessingStateDouble State { get; set; }
		private LfMergeSettings Settings { get; set; }

		public ProcessingStateFactoryDouble(LfMergeSettings settings)
		{
			Settings = settings;
		}

		#region IProcessingStateDeserialize implementation
		public ProcessingState Deserialize(string projectCode)
		{
			if (State == null)
				State = new ProcessingStateDouble(projectCode, Settings);
			return State;
		}
		#endregion
	}

	public class ProcessingStateDouble: ProcessingState
	{
		public List<ProcessingState.SendReceiveStates> SavedStates;

		public ProcessingStateDouble(string projectCode, LfMergeSettings settings): base(projectCode, settings)
		{
			SavedStates = new List<ProcessingState.SendReceiveStates>();
		}

		protected override void SetProperty<T>(ref T property, T value)
		{
			property = value;

			if (SavedStates.Count == 0 || SavedStates[SavedStates.Count - 1] != SRState)
				SavedStates.Add(SRState);
		}

		public void ResetSavedStates()
		{
			SavedStates.Clear();
		}
	}

	public class LanguageForgeProjectAccessor: LanguageForgeProject
	{
		protected LanguageForgeProjectAccessor(): base(null)
		{
		}

		public static void Reset()
		{
			LanguageForgeProject.DisposeProjectCache();
		}
	}

	public class LfMergeSettingsDouble: LfMergeSettings
	{
		static LfMergeSettingsDouble()
		{
			ConfigDir = Path.GetRandomFileName();
		}

		public LfMergeSettingsDouble(string replacementBaseDir) : base()
		{
			var replacementConfig = new IniData(ParsedConfig);
			replacementConfig.Global["BaseDir"] = replacementBaseDir;
			Initialize(replacementConfig);
			CommitWhenDone = false;
			VerboseProgress = true;
			PhpSourcePath = Path.Combine(TestEnvironment.FindGitRepoRoot(), "data/php/src");
		}
	}

	public class MongoConnectionDouble: IMongoConnection
	{
		public static void Initialize()
		{
			// Just as with MongoConnection.Initialize(), we need to set up BSON serialization conventions
			// so that the "fake" connection can deserialize the sample JSON identically to how the real DB does it.
			Console.WriteLine("Initializing FAKE Mongo connection...");

			// Serialize Boolean values permissively
			BsonSerializer.RegisterSerializationProvider(new BooleanSerializationProvider());

			// Use CamelCaseName conversions between Mongo and our mapping classes
			var pack = new ConventionPack();
			pack.Add(new CamelCaseElementNameConvention());
			ConventionRegistry.Register(
				"My Custom Conventions",
				pack,
				t => t.FullName.StartsWith("LfMerge."));

			// Register class mappings before opening first connection
			new MongoRegistrarForLfConfig().RegisterClassMappings();
		}

		private readonly Dictionary<string, LfInputSystemRecord> _storedInputSystems = new Dictionary<string, LfInputSystemRecord>();
		private readonly Dictionary<Guid, LfLexEntry> _storedLfLexEntries = new Dictionary<Guid, LfLexEntry>();
		private readonly Dictionary<string, LfOptionList> _storedLfOptionLists = new Dictionary<string, LfOptionList>();
		private Dictionary<string, LfConfigFieldBase> _storedCustomFieldConfig = new Dictionary<string, LfConfigFieldBase>();
		private Dictionary<string, DateTime?> _storedLastSyncDate = new Dictionary<string, DateTime?>();

		public void Reset()
		{
			_storedInputSystems.Clear();
			_storedLfLexEntries.Clear();
			_storedLfOptionLists.Clear();
			_storedCustomFieldConfig.Clear();
		}

		public long EntryCount(ILfProject project) { return _storedLfLexEntries.LongCount(); }

		public static TObj DeepCopy<TObj>(TObj orig)
		{
			// Take advantage of BSON serialization to clone any object
			// This allows this test double to act a bit more like a "real" Mongo database: it no longer holds
			// onto references to the objects that the test code added. Instead, it clones them. So if we change
			// fields of those objects *after* adding them to Mongo, those changes should NOT be reflected in Mongo.
			BsonDocument bson = orig.ToBsonDocument();
			return BsonSerializer.Deserialize<TObj>(bson);
		}

		public Dictionary<string, LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			return _storedInputSystems;
		}

		public bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			List<string> vernacularWss, List<string> analysisWss, List<string> pronunciationWss)
		{
			foreach (var ws in inputSystems.Keys)
				_storedInputSystems[ws] = inputSystems[ws];

			if (project.IsInitialClone)
			{
				// TODO: Update field input systems too?
			}
			return true;
		}

		public bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			if (lfCustomFieldList == null)
				_storedCustomFieldConfig = new Dictionary<string, LfConfigFieldBase>();
			else
				// _storedCustomFieldConfig = lfCustomFieldList; // This would assign a reference; we want to clone instead, in case unit tests further modify this dict
				_storedCustomFieldConfig = new Dictionary<string, LfConfigFieldBase>(lfCustomFieldList); // Cloning better simulates writing to Mongo
			return true;
		}

		public Dictionary<string, LfConfigFieldBase> GetCustomFieldConfig(ILfProject project)
		{
			return _storedCustomFieldConfig;
		}

		public void UpdateMockLfLexEntry(BsonDocument mockData)
		{
			LfLexEntry data = BsonSerializer.Deserialize<LfLexEntry>(mockData);
			UpdateMockLfLexEntry(data);
		}

		public void UpdateMockLfLexEntry(LfLexEntry mockData)
		{
			Guid guid = mockData.Guid ?? Guid.Empty;
			_storedLfLexEntries[guid] = DeepCopy(mockData);
		}

		public void UpdateMockOptionList(BsonDocument mockData)
		{
			LfOptionList data = BsonSerializer.Deserialize<LfOptionList>(mockData);
			UpdateMockOptionList(data);
		}

		public void UpdateMockOptionList(LfOptionList mockData)
		{
			string listCode = mockData.Code ?? string.Empty;
			_storedLfOptionLists[listCode] = DeepCopy(mockData);
		}

		public IEnumerable<LfLexEntry> GetLfLexEntries()
		{
			return new List<LfLexEntry>(_storedLfLexEntries.Values.Select(entry => DeepCopy(entry)));
		}

		public LfLexEntry GetLfLexEntryByGuid(Guid key)
		{
			LfLexEntry result;
			if (_storedLfLexEntries.TryGetValue(key, out result))
				return result;
			else
				return null;
		}

		public IEnumerable<LfOptionList> GetLfOptionLists()
		{
			return new List<LfOptionList>(_storedLfOptionLists.Values.Select(entry => DeepCopy(entry)));
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			switch (collectionName)
			{
			case MagicStrings.LfCollectionNameForLexicon:
				return (IEnumerable<TDocument>)GetLfLexEntries();
			case MagicStrings.LfCollectionNameForOptionLists:
				return (IEnumerable<TDocument>)GetLfOptionLists();
			default:
				List<TDocument> empty = new List<TDocument>();
				return empty.AsEnumerable();
			}
		}

		public Dictionary<Guid, DateTime> GetAllModifiedDatesForEntries(ILfProject project)
		{
			return _storedLfLexEntries.ToDictionary(kv => kv.Key, kv => kv.Value.AuthorInfo.ModifiedDate);
		}

		public LfOptionList GetLfOptionListByCode(ILfProject project, string listCode)
		{
			LfOptionList result;
			if (!_storedLfOptionLists.TryGetValue(listCode, out result))
				result = null;
			return result;
		}

		public IMongoDatabase GetProjectDatabase(ILfProject project)
		{
			var mockDb = new Mock<IMongoDatabase>(); // SO much easier than implementing the 9 public methods for a manual stub of IMongoDatabase!
			// TODO: Add appropriate mock functions if needed
			return mockDb as IMongoDatabase;
		}

		public IMongoDatabase GetMainDatabase()
		{
			var mockDb = new Mock<IMongoDatabase>(); // SO much easier than implementing the 9 public methods for a manual stub of IMongoDatabase!
			// TODO: Add appropriate mock functions if needed
			return mockDb as IMongoDatabase;
		}

		public bool UpdateRecord(ILfProject project, LfLexEntry data)
		{
			_storedLfLexEntries[data.Guid ?? Guid.Empty] = DeepCopy(data);
			return true;
		}

		public bool UpdateRecord(ILfProject project, LfOptionList data, string listCode)
		{
			_storedLfOptionLists[listCode ?? string.Empty] = DeepCopy(data);
			return true;
		}

		public bool RemoveRecord(ILfProject project, Guid guid)
		{
			_storedLfLexEntries.Remove(guid);
			return true;
		}

		public bool SetLastSyncedDate(ILfProject project, DateTime? newSyncedDate)
		{
			// No-op
			_storedLastSyncDate[project.ProjectCode] = newSyncedDate;
			return true;
		}

		public DateTime? GetLastSyncedDate(ILfProject project)
		{
			DateTime? result = null;
			if (_storedLastSyncDate.TryGetValue(project.ProjectCode, out result))
				return result;
			return null;
		}
	}

	public class MongoProjectRecordFactoryDouble: MongoProjectRecordFactory
	{
		public MongoProjectRecordFactoryDouble(IMongoConnection connection) : base(connection)
		{
		}

		public override MongoProjectRecord Create(ILfProject project)
		{
			var sampleConfig = BsonSerializer.Deserialize<LfProjectConfig>(SampleData.jsonConfigData);

			// TODO: Could we use a Mock to do this instead?
			return new MongoProjectRecord {
				Id = new ObjectId(),
				InputSystems = new Dictionary<string, LfInputSystemRecord>() {
					{"en", new LfInputSystemRecord {
							Abbreviation = "Eng",
							Tag = "en",
							LanguageName = "English",
							IsRightToLeft = false } },
					{"fr", new LfInputSystemRecord {
							// this should probably be a three-letter abbreviation like Fre,
							// but since our test data has the two letter abbreviation for this ws
							// we have to stick with it so that we don't introduce an unwanted
							// change.
							Abbreviation = "fr",
							Tag = "fr",
							LanguageName = "French",
							IsRightToLeft = false } },
				},
				InterfaceLanguageCode = "en",
				LanguageCode = "fr",
				ProjectCode = project.ProjectCode,
				ProjectName = project.ProjectCode,
				SendReceiveProjectIdentifier = null,
				Config = sampleConfig
			};
		}
	}

	class LanguageDepotProjectDouble: ILanguageDepotProject
	{
		#region ILanguageDepotProject implementation
		public void Initialize(string lfProjectCode)
		{
			Identifier = lfProjectCode;
		}

		public string Identifier { get; set; }
		public string Repository { get; set; }
		#endregion
	}

	class ChorusHelperDouble: ChorusHelper
	{
		public override string GetSyncUri(ILfProject project)
		{
			var server = LanguageDepotMock.Server;
			return server != null && server.IsStarted ? server.Url : LanguageDepotMock.ProjectFolderPath;
		}
	}

	class EnsureCloneActionDouble: EnsureCloneAction
	{
		private readonly bool _projectExists;

		public EnsureCloneActionDouble(LfMergeSettings settings, ILogger logger,
			MongoProjectRecordFactory projectRecordFactory, IMongoConnection connection, bool projectExists = true):
			base(settings, logger, projectRecordFactory, connection)
		{
			_projectExists = projectExists;
		}

		protected override bool CloneRepo(ILfProject project, string projectFolderPath,
			out string cloneResult)
		{
			if (_projectExists)
			{
				Directory.CreateDirectory(projectFolderPath);
				Directory.CreateDirectory(Path.Combine(projectFolderPath, ".hg"));
				File.WriteAllText(Path.Combine(projectFolderPath, ".hg", "hgrc"), "blablabla");
				cloneResult = string.Format("Clone success: new clone created on branch '' in folder {0}",
					projectFolderPath);
				return true;
			}
			throw new Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException();
		}
	}
}

// We can't directly use Chorus, so we redefine the exception we need here
namespace Chorus.VcsDrivers.Mercurial
{
	class RepositoryAuthorizationException: Exception
	{
	}
}
