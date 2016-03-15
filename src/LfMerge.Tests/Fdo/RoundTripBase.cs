// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
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

namespace LfMerge.Tests.Fdo
{
	public class RoundTripBase : FdoTestBase
	{
		public const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";

		protected IDictionary<string, Tuple<string, string>> GetMongoDifferences(
			BsonDocument itemBeforeTest,
			BsonDocument itemAfterTest
		)
		{
			var fieldNamesThatShouldBeDifferent = new string[] {
				"dateCreated",
				"dateModified",
				"createdDate",
				"modifiedDate",
			};
			var fieldNamesThatAreSubdocuments = new string[] {
				"authorInfo",
			};
			var differencesByName = new Dictionary<string, Tuple<string, string>>(); // Tuple of (before, after)
			foreach (var field in itemAfterTest)
			{
				if (fieldNamesThatShouldBeDifferent.Contains(field.Name))
					continue;

				if (fieldNamesThatAreSubdocuments.Contains(field.Name))
				{
					IDictionary<string, Tuple<string, string>> subDocumentDifferences;
					subDocumentDifferences = GetMongoDifferencesInSubDocument(itemBeforeTest, itemAfterTest, field.Name);
					foreach (var subField in subDocumentDifferences)
					{
						string subFieldName = subField.Key;
						differencesByName[field.Name + "." + subFieldName] = subField.Value;
					}
					continue;
				}

				if (!itemBeforeTest.Contains(field.Name))
				{
					differencesByName[field.Name] = new Tuple<string, string>(
						null,
						field.Value == null ? null : field.Value.ToString()
					);
				}
				else if (field.Value != itemBeforeTest[field.Name])
				{
					differencesByName[field.Name] = new Tuple<string, string>(
						itemBeforeTest[field.Name].ToString(),
						field.Value == null ? null : field.Value.ToString()
					);
				}
			}

			return differencesByName;
		}

		protected IDictionary<string, Tuple<string, string>> GetMongoDifferencesInSubDocument(
			BsonDocument parentDocumentBeforeTest,
			BsonDocument parentDocumentAfterTest,
			string fieldName
		)
		{
			var emptyDict = new Dictionary<string, Tuple<string, string>>();
			BsonDocument emptyBsonDoc = new BsonDocument();
			BsonValue subDocumentBeforeTest = parentDocumentBeforeTest.GetValue(fieldName, emptyBsonDoc);
			BsonValue subDocumentAfterTest = parentDocumentAfterTest.GetValue(fieldName, emptyBsonDoc);
			if (subDocumentBeforeTest.BsonType != BsonType.Document)
				return emptyDict;
			if (subDocumentAfterTest.BsonType != BsonType.Document)
				return emptyDict;
			return GetMongoDifferences(subDocumentBeforeTest.AsBsonDocument, subDocumentAfterTest.AsBsonDocument);
		}

		// Useful to call right before checking if difference dict is empty. Will only produce output if there are any differences.
		protected void PrintDifferences(IDictionary<string, Tuple<string, string>> differences)
		{
			foreach (var diff in differences)
				Console.WriteLine("{0}: {1} => {2}", diff.Key, diff.Value.Item1, diff.Value.Item2);
		}

		protected string Repr(object value)
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
		protected IDictionary<int, object> GetFieldValues(FdoCache cache, ICmObject obj)
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

		protected BsonDocument GetCustomFieldValues(FdoCache cache, ICmObject obj, string objectType = "entry")
		{
			// The objectType parameter is used in the names of the custom fields (and nowhere else).
			var convertCustomField = new ConvertFdoToMongoCustomField(cache, new LfMerge.Logging.NullLogger());
			Dictionary<string, LfConfigFieldBase> lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			return convertCustomField.GetCustomFieldsForThisCmObject(obj, objectType, lfCustomFieldList);
		}

		protected IDictionary<string, object> GetFieldValuesByName(FdoCache cache, ICmObject obj)
		{
			IFwMetaDataCacheManaged mdc = cache.ServiceLocator.MetaDataCache;
			return GetFieldValues(cache, obj).ToDictionary(kv => mdc.GetFieldName(kv.Key), kv => kv.Value);
		}

		protected IDictionary<string, Tuple<string, string>> GetFdoDifferences(
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

	}
}
