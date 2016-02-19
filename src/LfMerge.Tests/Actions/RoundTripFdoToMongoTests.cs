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
	[TestFixture, Explicit, Category("LongRunning")]
	public class RoundTripFdoToMongoTests : RoundTripBase
	{
		private string Repr(object value)
		{
			if (value == null)
				return "null";
			Type t = value.GetType();
			if (t == typeof(int[]))
				return "[" + String.Join(",", value as int[]) + "]";
			var tsString = value as ITsString;
			if (tsString != null)
				return tsString.Text;
			var multi = value as IMultiAccessorBase;
			if (multi != null)
			{
				int[] writingSystemIds;
				try {
					writingSystemIds = multi.AvailableWritingSystemIds;
				} catch (NotImplementedException) {
					// It's probably a VirtualStringAccessor, which we can't handle. Punt.
					return value.ToString();
				}
				var sb = new System.Text.StringBuilder();
				sb.Append("{ ");
				foreach (int ws in multi.AvailableWritingSystemIds)
				{
					sb.Append(ws);
					sb.Append(": ");
					sb.Append(multi.get_String(ws).Text);
					sb.Append(", ");
				}
				sb.Append("}");
				return sb.ToString();
			}
			return value.ToString();
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
//				"FullReferenceName",
//				"HeadWord",
//				"HeadWordRef",
//				"HeadWordReversal",
//				"LexSenseOutline",
//				"MLHeadWord",
//				"MLOwnerOutlineName",
//				"ReversalEntriesBulkText",
//				"ReversalName",
			};
			var differencesByName = new Dictionary<string, Tuple<string, string>>(); // Tuple of (before, after)
			foreach (int flid in fieldValuesBeforeTest.Keys)
			{
				if (mdc.IsCustom(flid))
					continue;
				string fieldName = mdc.GetFieldNameOrNull(flid);
				if (String.IsNullOrEmpty(fieldName))
					continue;
				object valueBeforeTest = fieldValuesBeforeTest[flid];
				object valueAfterTest = fieldValuesAfterTest[flid];

				// Some fields, like DateModified, *should* be different
				if (fieldNamesThatShouldBeDifferent.Contains(fieldName) ||
					fieldNamesToSkip.Contains(fieldName))
					continue;

				if ((valueAfterTest == null && valueBeforeTest == null))
					continue;
				if (mdc.GetFieldType(flid) == (int)CellarPropertyType.String)
				{
					// Might not need this, see below
				}

				// Arrays need to be compared specially
				Type valueType = valueBeforeTest.GetType();
				if (valueType == typeof(int[]))
				{
					int[] before = valueBeforeTest as int[];
					int[] after = valueAfterTest as int[];
					if (before.SequenceEqual(after))
						continue;
				}
				// So do TsString objects
				var tsStringBeforeTest = valueBeforeTest as ITsString;
				var tsStringAfterTest = valueAfterTest as ITsString;
				if (tsStringBeforeTest != null && tsStringAfterTest != null)
				{
					if (tsStringBeforeTest.Text != tsStringAfterTest.Text)
					{
						differencesByName[fieldName] = new Tuple<string, string>(
							tsStringBeforeTest.Text,
							tsStringAfterTest.Text
						);
					}
					continue;
				}
				// So do multistrings
				var multiStrBeforeTest = valueBeforeTest as IMultiAccessorBase;
				var multiStrAfterTest = valueAfterTest as IMultiAccessorBase;
				if (multiStrBeforeTest != null && multiStrAfterTest != null)
				{
					int[] wsIds;
					try
					{
						wsIds = multiStrBeforeTest.AvailableWritingSystemIds;
					}
					catch (NotImplementedException)
					{
						// This is a VirtualStringAccessor, which we can't easily compare. Punt.
						continue;
					}
					foreach (int wsId in wsIds)
					{
						string beforeStr = multiStrBeforeTest.get_String(wsId).Text;
						string afterStr = multiStrAfterTest.get_String(wsId).Text;
						if (beforeStr != afterStr)
						{
							string wsStr = cache.WritingSystemFactory.GetStrFromWs(wsId);
							differencesByName[fieldName + ":" + wsStr] = new Tuple<string, string>(beforeStr, afterStr);
							Console.WriteLine("After test, field {0} named {1} had value {2} of writing system {3}",
								flid, fieldName, afterStr, wsStr);
						}
					}
					continue;
				}
				if (valueBeforeTest.Equals(valueAfterTest))
					continue;
//				if (Repr(valueBeforeTest) == Repr(valueAfterTest))
//					continue; // This should catch TsStrings
				// If we get this far, they're different
				var diff = new Tuple<string, string>(Repr(fieldValuesBeforeTest[flid]), Repr(fieldValuesAfterTest[flid]));
				differencesByName[fieldName] = diff;
				Console.WriteLine("After test, field {0} named {1} had value {2} of type {3}",
					flid, fieldName, Repr(valueAfterTest), (valueAfterTest == null) ? "null" : valueAfterTest.GetType().ToString());
			}
			return differencesByName;
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepOriginalValuesInEntries()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Save field values before test, to compare with values after test
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			// Exercise
			sutFdoToMongo.Run(lfProj);
			sutMongoToFdo.Run(lfProj);

			// Verify
			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);

			IDictionary<string, Tuple<string, string>> differencesByName = GetDifferences(cache, fieldValues, fieldValuesAfterTest);

			// Special case: Ignore one particular GUID change, because our handling of
			// custom multi-paragraph fields is not yet perfect (some LF model changes
			// would be necessary to make it perfect).
			// TODO: Once we improve our handling of custom multi-paragraph fields, restore this check
			// by removing the code that ignores one particular field's GUID.
			customFieldValues["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");
			customFieldValuesAfterTest["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");

			Assert.That(differencesByName, Is.Empty);
			Assert.That(customFieldValues, Is.EqualTo(customFieldValuesAfterTest));
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepOriginalValuesInSenses()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense[] senses = entry.SensesOS.ToArray();
			Assert.That(senses.Length, Is.EqualTo(2));

			BsonDocument[] customFieldValues = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValues = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			// Exercise
			sutFdoToMongo.Run(lfProj);
			sutMongoToFdo.Run(lfProj);

			// Verify
			BsonDocument[] customFieldValuesAfterTest = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			var differencesByName1 = GetDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepOriginalValuesInExampleSentences()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			// Have to do it that way, because weirdly, the following line gets First() from MongoDB.Driver.Core!??!
			// ILexSense senseWithExamples = entry.SensesOS.First(sense => sense.ExamplesOS.Count > 0);
			ILexExampleSentence[] examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));

			BsonDocument[] customFieldValues = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValues = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			// Exercise
			sutFdoToMongo.Run(lfProj);
			sutMongoToFdo.Run(lfProj);

			// Verify
			BsonDocument[] customFieldValuesAfterTest = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			var differencesByName1 = GetDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepModifiedValuesInEntries()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					entry.CitationForm.SetVernacularDefaultWritingSystem("New value for this test");
				});
			cache.ActionHandlerAccessor.Commit();

			// Save field values before test, to compare with values after test
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			// Exercise
			sutFdoToMongo.Run(lfProj);
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					entry.CitationForm.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToFdo");
				});
			cache.ActionHandlerAccessor.Commit();
			sutMongoToFdo.Run(lfProj);

			// Verify
			Assert.That(entry.CitationForm.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToFdo"));
			Assert.That(entry.CitationForm.VernacularDefaultWritingSystem.Text, Is.EqualTo("New value for this test"));

			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);
			IDictionary<string, Tuple<string, string>> differencesByName = GetDifferences(cache, fieldValues, fieldValuesAfterTest);

			// Special case: Ignore one particular GUID change, because our handling of
			// custom multi-paragraph fields is not yet perfect (some LF model changes
			// would be necessary to make it perfect).
			// TODO: Once we improve our handling of custom multi-paragraph fields, restore this check
			// by removing the code that ignores one particular field's GUID.
			customFieldValues["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");
			customFieldValuesAfterTest["customFieldGuids"].AsBsonDocument.Remove("customField_entry_Cust_MultiPara");

			Assert.That(differencesByName, Is.Empty);
			Assert.That(customFieldValues, Is.EqualTo(customFieldValuesAfterTest));
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepModifiedValuesInSenses()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense[] senses = entry.SensesOS.ToArray();
			Assert.That(senses.Length, Is.EqualTo(2));
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					senses[0].AnthroNote.SetAnalysisDefaultWritingSystem("New value for this test");
					senses[1].AnthroNote.SetAnalysisDefaultWritingSystem("Second value for this test");
				});
			cache.ActionHandlerAccessor.Commit();

			BsonDocument[] customFieldValues = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValues = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			// Exercise
			sutFdoToMongo.Run(lfProj);
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					senses[0].AnthroNote.SetAnalysisDefaultWritingSystem("This value should be overwritten by MongoToFdo");
					senses[1].AnthroNote.SetAnalysisDefaultWritingSystem("This value should be overwritten by MongoToFdo");
				});
			cache.ActionHandlerAccessor.Commit();
			sutMongoToFdo.Run(lfProj);

			// Verify
			Assert.That(senses[0].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToFdo"));
			Assert.That(senses[1].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToFdo"));
			Assert.That(senses[0].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.EqualTo("New value for this test"));
			Assert.That(senses[1].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.EqualTo("Second value for this test"));

			BsonDocument[] customFieldValuesAfterTest = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();
			var differencesByName1 = GetDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

		[Test]
		public void RoundTrip_FdoToMongoToFdo_ShouldKeepModifiedValuesInExampleSentences()
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			var cache = lfProj.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(testEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			// Have to do it that way, because weirdly, the following line gets First() from MongoDB.Driver.Core!??!
			// ILexSense senseWithExamples = entry.SensesOS.First(sense => sense.ExamplesOS.Count > 0);
			ILexExampleSentence[] examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					examples[0].Example.SetVernacularDefaultWritingSystem("New value for this test");
					examples[1].Example.SetVernacularDefaultWritingSystem("Second value for this test");
				});
			cache.ActionHandlerAccessor.Commit();
			Console.WriteLine("FDO Example just got manually set to {0} for GUID {1} and HVO {2}",
				examples[0].Example.BestAnalysisVernacularAlternative.Text,
				examples[0].Guid,
				examples[0].Hvo
			);

			BsonDocument[] customFieldValues = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValues = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			// Exercise
			sutFdoToMongo.Run(lfProj);
			entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
				{
					examples[0].Example.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToFdo");
					examples[1].Example.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToFdo");
				});
			cache.ActionHandlerAccessor.Commit();
			Console.WriteLine("FDO Example just got manually and wrongly set to {0} for GUID {1} and HVO {2}",
				examples[0].Example.BestAnalysisVernacularAlternative.Text,
				examples[0].Guid,
				examples[0].Hvo
			);
			sutMongoToFdo.Run(lfProj);

			// Verify
			entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			Assert.That(examples[0].Example.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToFdo"));
			Assert.That(examples[1].Example.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToFdo"));
			Assert.That(examples[0].Example.VernacularDefaultWritingSystem.Text, Is.EqualTo("New value for this test"));
			Assert.That(examples[1].Example.VernacularDefaultWritingSystem.Text, Is.EqualTo("Second value for this test"));

			BsonDocument[] customFieldValuesAfterTest = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = examples.Select(example => GetFieldValues(cache, example)).ToArray();
			var differencesByName1 = GetDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

	}
}
