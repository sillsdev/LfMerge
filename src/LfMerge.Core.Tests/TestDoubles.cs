// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Autofac;
using Bugsnag.Payload;
using LfMergeBridge.LfMergeModel;
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
using Exception = System.Exception;

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
		public LfMergeSettingsDouble(string replacementBaseDir)
		{
			System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_BaseDir, replacementBaseDir);
			System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_VerboseProgress, "true");
			base.Initialize();
			CommitWhenDone = false;
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
				t => t.FullName.StartsWith("LfMerge.") || t.FullName.StartsWith("LfMergeBridge.LfMergeModel"));

			// Register class mappings before opening first connection
			new MongoRegistrarForLfConfig().RegisterClassMappings();
		}

		private readonly Dictionary<string, LfInputSystemRecord> _storedInputSystems = new Dictionary<string, LfInputSystemRecord>();
		// Held as documents, as Mongo holds them: an entry LF wrote keeps what LfMerge's own
		// serializer would leave out, such as a multitext holding an empty value
		private readonly Dictionary<Guid, BsonDocument> _storedLfLexEntries = new Dictionary<Guid, BsonDocument>();
		private readonly Dictionary<string, LfOptionList> _storedLfOptionLists = new Dictionary<string, LfOptionList>();
		private Dictionary<string, LfConfigFieldBase> _storedCustomFieldConfig = new Dictionary<string, LfConfigFieldBase>();
		private Dictionary<string, DateTime?> _storedLastSyncDate = new Dictionary<string, DateTime?>();

		// Used in mocking SetLastSyncDate
		private MongoProjectRecordFactoryDouble _projectRecordFactory = null;

		public void Reset()
		{
			_storedInputSystems.Clear();
			_storedLfLexEntries.Clear();
			_storedLfOptionLists.Clear();
			_storedCustomFieldConfig.Clear();
		}

		public void RegisterProjectRecordFactory(MongoProjectRecordFactoryDouble projectRecordFactory)
		{
			_projectRecordFactory = projectRecordFactory;
		}

		public long LexEntryCount(ILfProject project) { return _storedLfLexEntries.LongCount(); }

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

		/// <summary>The current vernacular, analysis and pronunciation lists the last export gave.</summary>
		public List<string> LastVernacularWss { get; private set; }
		public List<string> LastAnalysisWss { get; private set; }
		public List<string> LastPronunciationWss { get; private set; }

		public bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems,
			List<string> vernacularWss, List<string> analysisWss, List<string> pronunciationWss)
		{
			foreach (var ws in inputSystems.Keys)
				_storedInputSystems[ws] = inputSystems[ws];
			LastVernacularWss = vernacularWss;
			LastAnalysisWss = analysisWss;
			LastPronunciationWss = pronunciationWss;

			if (project.IsInitialClone)
			{
				// TODO: Update field input systems too?
			}
			return true;
		}

		public bool SetCustomFieldConfig(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList, Dictionary<string, string> lfCustomFieldTypes)
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

		// The user SampleData's entries were last modified by
		private static readonly ObjectId LfUserRef = ObjectId.Parse("561b666c0f87096a35c3cf2d");

		/// <summary>
		/// Store an entry the way an LF user's edit does: LF's MapperModel::write stamps DateModified,
		/// and LexEntryCommands::updateEntry records who made the edit. LfMerge's own writes go
		/// through UpdateRecord, which does neither. The document is stored as given, empty values
		/// and all, since LF writes what its editor sends.
		/// </summary>
		public void UpdateMockLfLexEntry(BsonDocument mockData)
		{
			var stored = (BsonDocument)mockData.DeepClone();
			Guid guid = BsonSerializer.Deserialize<LfLexEntry>(stored).Guid ?? Guid.Empty;
			stored["dateModified"] = new BsonDateTime(DateTime.UtcNow);
			BsonValue authorInfo;
			if (stored.TryGetValue("authorInfo", out authorInfo) && authorInfo.IsBsonDocument)
			{
				BsonValue userRef;
				if (!authorInfo.AsBsonDocument.TryGetValue("modifiedByUserRef", out userRef) || userRef.IsBsonNull)
					authorInfo.AsBsonDocument["modifiedByUserRef"] = LfUserRef;
			}
			_storedLfLexEntries[guid] = stored;
		}

		/// <summary>As UpdateMockLfLexEntry(BsonDocument), for an entry built as an object.</summary>
		public void UpdateMockLfLexEntry(LfLexEntry mockData)
		{
			UpdateMockLfLexEntry(mockData.ToBsonDocument());
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
			return new List<LfLexEntry>(_storedLfLexEntries.Values.Select(entry => BsonSerializer.Deserialize<LfLexEntry>(entry)));
		}

		public LfLexEntry GetLfLexEntryByGuid(ILfProject _project, Guid key)
		{
			BsonDocument result;
			if (_storedLfLexEntries.TryGetValue(key, out result))
				return BsonSerializer.Deserialize<LfLexEntry>(result);
			return null;
		}

		public IEnumerable<LfOptionList> GetLfOptionLists()
		{
			return new List<LfOptionList>(_storedLfOptionLists.Values.Select(entry => DeepCopy(entry)));
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName, Expression<Func<TDocument, bool>> filter)
		{
			// A filtered request finds a few records, not a pass over the collection, so it is not
			// counted in LexiconReads
			return Records<TDocument>(collectionName).Where(filter.Compile());
		}

		/// <summary>
		/// How many times the whole lexicon has been asked for, each request being a pass over
		/// Mongo. Filtered requests, such as the comment converter's one per comment, are not
		/// counted.
		/// </summary>
		public int LexiconReads { get; private set; }

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			if (collectionName == MagicStrings.LfCollectionNameForLexicon)
				LexiconReads++;
			return Records<TDocument>(collectionName);
		}

		private IEnumerable<TDocument> Records<TDocument>(string collectionName)
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
			return _storedLfLexEntries.ToDictionary(kv => kv.Key,
				kv => BsonSerializer.Deserialize<LfLexEntry>(kv.Value).AuthorInfo.ModifiedDate);
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
			_storedLfLexEntries[data.Guid ?? Guid.Empty] = data.ToBsonDocument();
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
			// Mongo keeps dates to the millisecond, as DeepCopy does for the entries' dates
			if (newSyncedDate != null)
				newSyncedDate = newSyncedDate.Value.AddTicks(-(newSyncedDate.Value.Ticks % TimeSpan.TicksPerMillisecond));
			_storedLastSyncDate[project.ProjectCode] = newSyncedDate;
			// Also update on the fake project record, since EnsureCloneAction looks at the project record to check its initial-clone logic
			if (_projectRecordFactory != null)
			{
				MongoProjectRecord projectRecord = _projectRecordFactory.Create(project);
				projectRecord.LastSyncedDate = newSyncedDate;
			}
			return true;
		}

		public DateTime? GetLastSyncedDate(ILfProject project)
		{
			DateTime? result = null;
			if (_storedLastSyncDate.TryGetValue(project.ProjectCode, out result))
				return result;
			return null;
		}

		public IEnumerable<LfComment> GetComments(ILfProject project)
		{
			yield break;
		}

		public Dictionary<MongoDB.Bson.ObjectId, Guid> GetGuidsByObjectIdForCollection(ILfProject project, string collectionName)
		{
			return new Dictionary<MongoDB.Bson.ObjectId, Guid>();
		}

		public void UpdateComments(ILfProject project, List<LfComment> comments)
		{
			;
		}

		public void UpdateReplies(ILfProject project, List<Tuple<string, List<LfCommentReply>>> repliesFromFWWithCommentGuids)
		{
			;
		}

		public void UpdateCommentStatuses(ILfProject project, List<KeyValuePair<string, Tuple<string, string>>> statusChanges)
		{
			;
		}

		public void SetCommentReplyGuids(ILfProject project, IDictionary<string,Guid> uniqIdToGuidMappings)
		{
			// No-op. TODO: Implement something that stores a simple comment-replies data structure so we can unit test.
		}

		public void SetCommentGuids(ILfProject project, IDictionary<string,Guid> commentIdToGuidMappings)
		{
			// No-op. TODO: Implement something that stores a simple comments dictionary so we can unit test.
		}
	}

	public class MongoProjectRecordFactoryDouble: MongoProjectRecordFactory
	{
		// Memoize the project records for each project so that we're returning the same one each time, to better simulate the Mongo DB.
		private Dictionary<string, MongoProjectRecord> _projectRecords;
		public MongoProjectRecordFactoryDouble(IMongoConnection connection) : base(connection)
		{
			_projectRecords = new Dictionary<string, MongoProjectRecord>();
			var testDouble = connection as MongoConnectionDouble;
			if (testDouble != null)
			{
				testDouble.RegisterProjectRecordFactory(this);
			}
		}

		public override MongoProjectRecord Create(ILfProject project)
		{
			var sampleConfig = BsonSerializer.Deserialize<LfProjectConfig>(SampleData.jsonConfigData);

			// TODO: Could we use a Mock to do this instead?
			MongoProjectRecord record;
			if (_projectRecords.TryGetValue(project.ProjectCode, out record))
			{
				RefreshLastSyncedDate(record, project);
				return record;
			}
			else
			{
				record = new MongoProjectRecord {
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
					SendReceiveProjectIdentifier = project.ProjectCode,
					Config = sampleConfig
				};
				_projectRecords.Add(project.ProjectCode, record);
				RefreshLastSyncedDate(record, project);
				return record;
			}
		}

		/// The real factory reads the record from Mongo every time, so it always sees the last sync's
		/// date. Each instance of this double keeps its own records, and the connection double only
		/// updates the most recently created instance's, so take the date from the connection instead.
		private void RefreshLastSyncedDate(MongoProjectRecord record, ILfProject project)
		{
			var testDouble = Connection as MongoConnectionDouble;
			if (testDouble != null) record.LastSyncedDate = testDouble.GetLastSyncedDate(project);
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
			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			// Allow tests to override LanguageDepotRepoUri if necessary (e.g., the E2E tests which need ChorusHelperDouble but use a "real" LexBox instance)
			if (!string.IsNullOrEmpty(settings.LanguageDepotRepoUri))
				return settings.LanguageDepotRepoUri;
			var server = LanguageDepotMock.Server;
			return server != null && server.IsStarted ? server.Url : LanguageDepotMock.ProjectFolderPath;
		}
	}

	class EnsureCloneActionDouble: EnsureCloneAction
	{
		private readonly bool _projectExists;
		private readonly bool _throwAuthorizationException;

		public EnsureCloneActionDouble(LfMergeSettings settings, ILogger logger,
			MongoProjectRecordFactory projectRecordFactory, IMongoConnection connection,
			bool projectExists = true, bool throwAuthorizationException = true):
			base(settings, logger, projectRecordFactory, connection)
		{
			_projectExists = projectExists;
			_throwAuthorizationException = throwAuthorizationException;
		}

		protected override bool CloneRepo(ILfProject project, string projectFolderPath,
			out string cloneResult)
		{
			if (_projectExists)
			{
				Directory.CreateDirectory(projectFolderPath);
				Directory.CreateDirectory(Path.Combine(projectFolderPath, ".hg"));
				File.WriteAllText(Path.Combine(projectFolderPath, ".hg", "hgrc"), "blablabla");
				cloneResult =
					$"Clone success: new clone created on branch '' in folder {projectFolderPath}";
				return true;
			}
			if (_throwAuthorizationException)
				throw new Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException();

			throw new Exception("Just some arbitrary exception");
		}
	}

	class EnsureCloneActionDoubleMockingInitialTransfer: EnsureCloneActionDouble
	{
		// This mock object will be used in the EnsureClone_RunsInitialClone series of tests
		internal bool InitialCloneWasRun { get; set; }

		public EnsureCloneActionDoubleMockingInitialTransfer(LfMergeSettings settings, ILogger logger,
			MongoProjectRecordFactory projectRecordFactory, IMongoConnection connection, bool projectExists = true):
			base(settings, logger, projectRecordFactory, connection, projectExists)
		{
			InitialCloneWasRun = false;
		}

		protected override void InitialTransferToMongoAfterClone(ILfProject project)
		{
			InitialCloneWasRun = true;
		}
	}

	class EnsureCloneActionDoubleMockErrorCondition : EnsureCloneAction
	{
		private string _cloneResult;

		public EnsureCloneActionDoubleMockErrorCondition(LfMergeSettings settings, ILogger logger,
			MongoProjectRecordFactory projectRecordFactory, IMongoConnection connection,
			string cloneResult):
			base(settings, logger, projectRecordFactory, connection)
		{
			_cloneResult = cloneResult;
		}

		protected override bool CloneRepo(ILfProject project, string projectFolderPath, out string cloneResult)
		{
			cloneResult = _cloneResult;
			return true;
		}
	}

	class ExceptionLoggingDouble : ExceptionLogging
	{
		private List<Report> _exceptions = new List<Report>();

		private ExceptionLoggingDouble() : base("unit-test", "foo", ".")
		{
		}

		protected override void OnBeforeNotify(Report report)
		{
			_exceptions.Add(report);
		}

		public List<Report> Exceptions => _exceptions;

		public static ExceptionLoggingDouble Initialize()
		{
			var exceptionLoggingDouble = new ExceptionLoggingDouble();
			Client = exceptionLoggingDouble;
			return exceptionLoggingDouble;
		}
	}
}
