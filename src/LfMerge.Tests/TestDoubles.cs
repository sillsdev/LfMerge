// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chorus.Model;
using IniParser.Model;
using LfMerge.LanguageForge.Model;
using LfMerge.LanguageForge.Config;
using LfMerge.FieldWorks;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using SIL.Progress;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Moq;

namespace LfMerge.Tests
{
	public class ProcessingStateFactoryDouble: IProcessingStateDeserialize
	{
		public ProcessingStateDouble State { get; set; }
		private LfMergeSettingsIni Settings { get; set; }

		public ProcessingStateFactoryDouble(LfMergeSettingsIni settings)
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

		public ProcessingStateDouble(string projectCode, LfMergeSettingsIni settings): base(projectCode, settings)
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
		protected LanguageForgeProjectAccessor(LfMergeSettingsIni settings): base(settings, null)
		{
		}

		public static void Reset()
		{
			LanguageForgeProject.DisposeProjectCache();
		}
	}

	public class LfMergeSettingsDouble: LfMergeSettingsIni
	{
		public LfMergeSettingsDouble(string replacementBaseDir) : base()
		{
			var replacementConfig = new IniData(ParsedConfig);
			replacementConfig.Global["BaseDir"] = replacementBaseDir;
			Initialize(replacementConfig);
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
			new LfMerge.LanguageForge.Config.MongoRegistrarForLfConfig().RegisterClassMappings();
		}

		private Dictionary<string, Dictionary<Guid, object>> _storedDataByGuid = new Dictionary<string, Dictionary<Guid, object>>();
		private Dictionary<string, Dictionary<ObjectId, object>> _storedDataByObjectId = new Dictionary<string, Dictionary<ObjectId, object>>();

		// For use in unit tests that want to verify what was placed into Mongo
		public Dictionary<string, Dictionary<Guid, object>> StoredDataByGuid { get { return _storedDataByGuid; } }
		public Dictionary<string, Dictionary<ObjectId, object>> StoredDataByObjectId { get { return _storedDataByObjectId; } }

		private void EnsureCollectionExists(string collectionName)
		{
			try
			{
				_storedDataByGuid.Add(collectionName, new Dictionary<Guid, object>());
			}
			catch (ArgumentException)
			{
				// It's fine if it already exists
			}
			try
			{
				_storedDataByObjectId.Add(collectionName, new Dictionary<ObjectId, object>());
			}
			catch (ArgumentException)
			{
				// It's fine if it already exists
			}
		}

		public Dictionary<string, LfInputSystemRecord> GetInputSystems(ILfProject project)
		{
			return new Dictionary<string, LfInputSystemRecord>();
		}

		public bool SetInputSystems(ILfProject project, Dictionary<string, LfInputSystemRecord> inputSystems, bool initialClone = false, string vernacularWs = "", string analysisWs = "")
		{
			return true;
		}

		public void AddToMockData<TDocument>(string collectionName, BsonDocument mockData)
		{
			EnsureCollectionExists(collectionName);
			string guidStr = mockData.GetValue("guid", Guid.Empty.ToString()).AsString;
			ObjectId id = mockData.GetValue("_id", ObjectId.Empty).AsObjectId; // TODO: Breakpoint this and check if "_id" is the right name
			Guid guid = Guid.Parse(guidStr);
			TDocument data = BsonSerializer.Deserialize<TDocument>(mockData);
			_storedDataByGuid[collectionName][guid] = data;
			_storedDataByObjectId[collectionName][id] = data;
		}

		public void AddToMockData<TDocument>(string collectionName, TDocument data)
		{
			BsonDocument mockData = data.ToBsonDocument();
			AddToMockData<TDocument>(collectionName, mockData);
			// NOTE: Without the explicit <TDocument>, that would have just called this function
			// again as AddToMockData<BsonDocument>(collectionName, mockData), which would have led
			// to infinite recursion and a stack overflow.
		}

		private IEnumerable<TDocument> GetRecordsByGuid<TDocument>(ILfProject project, string collectionName)
		{
			Dictionary<Guid, object> fakeCollection = _storedDataByGuid[collectionName];
			foreach (object item in fakeCollection.Values)
			{
				yield return (TDocument)item;
			}
		}

		private IEnumerable<TDocument> GetRecordsByObjectId<TDocument>(ILfProject project, string collectionName)
		{
			Dictionary<ObjectId, object> fakeCollection = _storedDataByObjectId[collectionName];
			foreach (object item in fakeCollection.Values)
			{
				yield return (TDocument)item;
			}
		}

		public IEnumerable<TDocument> GetRecords<TDocument>(ILfProject project, string collectionName)
		{
			EnsureCollectionExists(collectionName);
			bool byGuid = false;
			if (collectionName == MagicStrings.LfCollectionNameForLexicon)
				byGuid = true;
			if (byGuid)
				return GetRecordsByGuid<TDocument>(project, collectionName);
			else
				return GetRecordsByObjectId<TDocument>(project, collectionName);
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
			return UpdateRecordImpl<LfLexEntry>(project, data, data.Guid, null, MagicStrings.LfCollectionNameForLexicon);
		}

		public bool UpdateRecord(ILfProject project, LfOptionList data, ObjectId id)
		{
			return UpdateRecordImpl<LfOptionList>(project, data, null, id, MagicStrings.LfCollectionNameForOptionLists);
		}

		public bool UpdateRecordImpl<TDocument>(ILfProject project, TDocument data, Guid? guid, ObjectId? id, string collectionName)
		{
			EnsureCollectionExists(collectionName);
			if (guid != null)
				_storedDataByGuid[collectionName][guid.Value] = data;
			if (id != null)
				_storedDataByObjectId[collectionName][id.Value] = data;
			return (guid != null) || (id != null);
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
							Abbreviation = "en",
							Tag = "en",
							LanguageName = "English",
							IsRightToLeft = false } },
					{"fr", new LfInputSystemRecord {
							Abbreviation = "fr",
							Tag = "fr",
							LanguageName = "French",
							IsRightToLeft = false } },
				},
				InterfaceLanguageCode = "en",
				LanguageCode = "fr",
				ProjectCode = project.LfProjectCode,
				ProjectName = project.FwProjectCode,
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

		public string Username { get; set; }
		public string Password { get; set; }
		public string Identifier { get; set; }
		public string Repository { get; set; }
		#endregion
	}

	class InternetCloneSettingsModelDouble: InternetCloneSettingsModel
	{
		public override void DoClone()
		{
			Directory.CreateDirectory(TargetDestination);
			Directory.CreateDirectory(Path.Combine(TargetDestination, ".hg"));
			File.WriteAllText(Path.Combine(TargetDestination, ".hg", "hgrc"), "blablabla");
		}
	}

	class UpdateBranchHelperFlexDouble: UpdateBranchHelperFlex
	{
		public override bool UpdateToTheCorrectBranchHeadIfPossible(string desiredBranchName,
			ActualCloneResult cloneResult, string cloneLocation)
		{
			cloneResult.FinalCloneResult = FinalCloneResult.Cloned;
			return true;
		}
	}

	class FlexHelperDouble: FlexHelper
	{
		public override void PutHumptyTogetherAgain(IProgress progress, bool verbose, string mainFilePathname)
		{
		}
	}
}
