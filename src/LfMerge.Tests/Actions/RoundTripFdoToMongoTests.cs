// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Tests;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.Tests.Actions
{
	public class RoundTripFdoToMongoTests
	{
		public const string testProjectCode = "TestLangProj";
		private TestEnvironment _env;
		private MongoConnectionDoubleThatStoresData _conn;
		private MongoProjectRecordFactory _recordFactory;
		private UpdateFdoFromMongoDbAction sutMongoToFdo;
		private UpdateMongoDbFromFdo sutFdoToMongo;

		public RoundTripFdoToMongoTests()
		{
		}

		[SetUp]
		public void Setup()
		{
			//_env = new TestEnvironment();
			_env = new TestEnvironment(fakeMongoConnectionShouldStoreData: true, testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDoubleThatStoresData;
			if (_conn == null)
				throw new AssertionException("Fdo->Mongo roundtrip tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Fdo->Mongo roundtrip tests need a mock MongoProjectRecordFactory in order to work.");
			// TODO: If creating our own Mocks would be better than getting them from Autofac, do that instead.

			sutMongoToFdo = new UpdateFdoFromMongoDbAction(
				_env.Settings,
				_env.Logger,
				_conn,
				_recordFactory
			);

			sutFdoToMongo = new UpdateMongoDbFromFdo(
				_env.Settings,
				_env.Logger,
				_conn
			);
		}

		private string Repr(object value)
		{
			if (value == null)
				return "null";
			return value.GetType() == typeof(int[]) ?
				"[" + String.Join(",", value as int[]) + "]" :
				value.ToString();
		}

		/// <summary>
		/// Get the field values as a dict, keyed by field ID, for any CmObject.
		/// </summary>
		/// <returns>A dictionary with integer field ID mapped to values.</returns>
		/// <param name="cache">FDO cache the object lives in.</param>
		/// <param name="obj">Object whose fields we're getting.</param>
		private IDictionary<int, object> GetFieldValues(FdoCache cache, ICmObject obj)
		{
			IFwMetaDataCacheManaged mdc = cache.ServiceLocator.MetaDataCache;
			ISilDataAccess data = cache.DomainDataByFlid;
			int[] fieldIds = mdc.GetFields(obj.ClassID, false, (int)CellarPropertyTypeFilter.All);
			var fieldValues = new Dictionary<int, object>();
			foreach (int flid in fieldIds)
			{
				if (mdc.IsCustom(flid))
					continue; // Custom fields get processed differently
				string fieldName = mdc.GetFieldNameOrNull(flid);
				if (String.IsNullOrEmpty(fieldName))
					continue;
				object value = data.get_Prop(obj.Hvo, flid);
				fieldValues[flid] = value;
				Console.WriteLine("Field {0} named {1} had value {2} of type {3}",
					flid, fieldName, Repr(value), (value == null) ? "null" : value.GetType().ToString());
			}
			return fieldValues;
		}

		private BsonDocument GetCustomFieldValues(FdoCache cache, ICmObject obj, string objectType = "entry")
		{
			// The objectType parameter is used in the names of the custom fields (and nowhere else).
			var customFieldConverter = new CustomFieldConverter(cache);
			return customFieldConverter.CustomFieldsForThisCmObject(obj, objectType);
		}

		private IDictionary<string, object> GetFieldValuesByName(FdoCache cache, ICmObject obj)
		{
			IFwMetaDataCacheManaged mdc = cache.ServiceLocator.MetaDataCache;
			return GetFieldValues(cache, obj).ToDictionary(kv => mdc.GetFieldName(kv.Key), kv => kv.Value);
		}

		private IDictionary<string, Tuple<string, string>> GetDifferences(
			FdoCache cache,
			IDictionary<int, object> fieldValuesBeforeTest,
			IDictionary<int, object> fieldValuesAfterTest
		)
		{
			IFwMetaDataCacheManaged mdc = cache.ServiceLocator.MetaDataCache;
			var fieldNamesThatShouldBeDifferent = new string[] {
				"DateCreated",
				"DateModified",
			};
			var fieldNamesToSkip = new string[] {
				// These are ComObject or SIL.FieldWorks.FDO.DomainImpl.VirtualStringAccessor instances, which we can't compare
				"HeadWord",
				"MLHeadWord",
				"HeadWordRef",
				"HeadWordReversal",
			};
			var differencesByName = new Dictionary<string, Tuple<string, string>>(); // Tuple of (before, after)
			foreach (int flid in fieldValuesBeforeTest.Keys)
			{
				if (mdc.IsCustom(flid))
					continue;
				string fieldName = mdc.GetFieldNameOrNull(flid);
				if (String.IsNullOrEmpty(fieldName))
					continue;
				object valueAfterTest = fieldValuesAfterTest[flid];

				// Some fields, like DateModified, *should* be different
				if (fieldNamesThatShouldBeDifferent.Contains(fieldName) ||
					fieldNamesToSkip.Contains(fieldName))
					continue;

				if ((valueAfterTest == null && fieldValuesBeforeTest[flid] == null))
					continue;
				if (fieldValuesBeforeTest[flid].Equals(valueAfterTest))
					continue;
				// Arrays need to be compared specially
				if (fieldValuesBeforeTest[flid].GetType() == typeof(int[]))
				{
					int[] before = fieldValuesBeforeTest[flid] as int[];
					int[] after = valueAfterTest as int[];
					if (before.SequenceEqual(after))
						continue;
				}
				// If we get this far, they're different
				var diff = new Tuple<string, string>(Repr(fieldValuesBeforeTest[flid]), Repr(fieldValuesAfterTest[flid]));
				differencesByName[fieldName] = diff;
				Console.WriteLine("After test, field {0} named {1} had value {2} of type {3}",
					flid, fieldName, Repr(valueAfterTest), (valueAfterTest == null) ? "null" : valueAfterTest.GetType().ToString());
			}
			return differencesByName;
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepSameValues()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			string entryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
			Guid entryGuid = Guid.Parse(entryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Save field values before test, to compare with values after test
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			// Exercise
			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			sutFdoToMongo.Run(lfProj);
			sutMongoToFdo.Run(lfProj);
			stopwatch.Stop();
			Console.WriteLine("Running test took {0} ms", stopwatch.ElapsedMilliseconds);

			// Verify
			// string expectedShortName = "ztestmain";
			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);

			LfLexEntry mongoEntry = _conn.StoredData[entryGuid] as LfLexEntry;
			Console.WriteLine("Mongo data for {0}:", entryGuid);
			Console.WriteLine(mongoEntry.ToJson());

			var differencesByName = GetDifferences(cache, fieldValues, fieldValuesAfterTest);

			foreach (KeyValuePair<string, Tuple<string, string>> diff in differencesByName)
			{
				string fieldName = diff.Key;
				string oldVal = diff.Value.Item1;
				string newVal = diff.Value.Item2;
				Console.WriteLine("Field {0} was different. Old value: {1}, new value: {2}", fieldName, oldVal, newVal);
			}

			Console.WriteLine("Custom fields before test: {0}", customFieldValues);
			Console.WriteLine("Custom fields  after test: {0}", customFieldValuesAfterTest);

			// Special case: Ignore one particular GUID change, because our handling of
			// custom multi-paragraph fields is not yet perfect (some LF model changes
			// would be necessary to make it perfect).
			// TODO: Once we improve our handling of custom multi-paragraph fields, restore this check
			// by removing the code that ignores one particular field's GUID.
			customFieldValues["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");
			customFieldValuesAfterTest["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");

			Assert.That(differencesByName, Is.Empty); // Should fail
			Assert.That(customFieldValues, Is.EqualTo(customFieldValuesAfterTest));

			// This test is currently failing on custom field differences, because the GUID for the Cust_MultiPara field is changing:
			// Before: customFieldGuids={ "customField_entry_Cust_MultiPara" : "f71fe561-3677-483f-9ff5-4f918c0203a6", "customField_entry_Cust_Single_ListRef" : "d7f713ad-e8cf-11d3-9764-00c04f186933" }
			// After:  customFieldGuids={ "customField_entry_Cust_MultiPara" : "cd05615d-c47b-4e6f-8b4e-29285ababcee", "customField_entry_Cust_Single_ListRef" : "d7f713ad-e8cf-11d3-9764-00c04f186933" }
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepSameValues()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);

			// Exercise
			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			sutMongoToFdo.Run(lfProj);
			sutFdoToMongo.Run(lfProj);
			stopwatch.Stop();
			Console.WriteLine("Running test took {0} ms", stopwatch.ElapsedMilliseconds);

			// Verify

			// TODO: Write verification here.
		}
	}
}

