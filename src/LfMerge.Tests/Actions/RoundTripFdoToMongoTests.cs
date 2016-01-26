// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.FieldWorks;
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
		private MongoConnectionDouble _conn;
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
			_env = new TestEnvironment(testProjectCode: testProjectCode);
			_conn = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_conn == null)
				throw new AssertionException("Fdo->Mongo roundtrip tests need a mock MongoConnection in order to work.");
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
			// TODO: This is really complex. Extract it to helper methods so we can write multiple
			// round-trip tests without duplicating all of this.
			IFwMetaDataCacheManaged mdc = cache.ServiceLocator.MetaDataCache;
			ISilDataAccess data = cache.DomainDataByFlid;
			int[] fieldsForLexEntry = mdc.GetFields(LexEntryTags.kClassId, false, (int)CellarPropertyTypeFilter.All);
//			var fieldValues = new Dictionary<int, object>();
//			var fieldValuesByName = new Dictionary<string, object>();
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			var sampleData = new SampleData();
			_conn.AddToMockData(sampleData.bsonTestData);

			// Exercise
			sutFdoToMongo.Run(lfProj);
			sutMongoToFdo.Run(lfProj);

			// Verify
			// string expectedShortName = "ztestmain";
			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);

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
			var differences = new Dictionary<int, Tuple<string, string>>(); // Tuple of (before, after)
			var differencesByName = new Dictionary<string, Tuple<string, string>>(); // Tuple of (before, after)
			foreach (int flid in fieldsForLexEntry)
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

				if ((valueAfterTest == null && fieldValues[flid] == null))
					continue;
				if (fieldValues[flid].Equals(valueAfterTest))
					continue;
				// Arrays need to be compared specially
				if (fieldValues[flid].GetType() == typeof(int[]))
				{
					int[] before = fieldValues[flid] as int[];
					int[] after = valueAfterTest as int[];
					if (before.SequenceEqual(after))
						continue;
				}
				// If we get this far, they're different
				var diff = new Tuple<string, string>(Repr(fieldValues[flid]), Repr(fieldValuesAfterTest[flid]));
				differences[flid] = diff;
				differencesByName[fieldName] = diff;
				Console.WriteLine("After test, field {0} named {1} had value {2} of type {3}",
					flid, fieldName, Repr(valueAfterTest), (valueAfterTest == null) ? "null" : valueAfterTest.GetType().ToString());
			}

			foreach (KeyValuePair<string, Tuple<string, string>> diff in differencesByName)
			{
				string fieldName = diff.Key;
				string oldVal = diff.Value.Item1;
				string newVal = diff.Value.Item2;
				Console.WriteLine("Field {0} was different. Old value: {1}, new value: {2}", fieldName, oldVal, newVal);
			}

			Console.WriteLine("Custom fields before test: {0}", customFieldValues);
			Console.WriteLine("Custom fields after test: {0}", customFieldValuesAfterTest);

			Assert.That(differencesByName, Is.Empty); // Should fail
			Assert.That(customFieldValues, Is.EqualTo(customFieldValuesAfterTest));

			// This test is currently failing on custom field differences, because GUIDs are changing:
			// Before: customFieldGuids={ "customField_entry_Cust_MultiPara" : "6d6e3f72-b0ce-4f2e-aa3b-824a6bac0101", "customField_entry_Cust_Single_ListRef" : "5364d32b-2b3a-4876-85e4-97b72a47be5d" }
			// After:  customFieldGuids={ "customField_entry_Cust_MultiPara" : "cd05615d-c47b-4e6f-8b4e-29285ababcee", "customField_entry_Cust_Single_ListRef" : "d7f713ad-e8cf-11d3-9764-00c04f186933" }
		}

		[Test]
		public void RoundTrip_MongoToFdoToMongo_ShouldKeepSameValues()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var data = new SampleData();

			_conn.AddToMockData(data.bsonTestData);

			// Exercise
			sutMongoToFdo.Run(lfProj);
			sutFdoToMongo.Run(lfProj);

			// Verify

			// TODO: Write verification here.
		}
	}
}

