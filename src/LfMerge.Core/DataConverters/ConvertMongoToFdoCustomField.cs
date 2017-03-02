// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToFdoCustomField
	{
		private FdoCache cache;
		private FwServiceLocatorCache servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;
		private ILogger logger;

		public ConvertMongoToFdoCustomField(FdoCache cache, FwServiceLocatorCache serviceLocator, ILogger logger)
		{
			this.cache = cache;
			this.servLoc = serviceLocator;
			this.fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			this.logger = logger;
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result;
			return Guid.TryParse(input, out result) ? result : default(Guid);
		}

		public ICmPossibilityList GetParentListForField(int flid)
		{
			Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
			if (parentListGuid == Guid.Empty)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				logger.Warning("No possibility list found for custom field {0} (field ID {1}); it will not be present in FDO", fieldName ?? "(name not found)", flid);
				return null;
				// TODO: If this happens, we're probably importing a newly-created possibility list, so we should
				// probably create it in FDO using ConvertMongoToFdoOptionList. Implementation needed.
			}
			return servLoc.GetInstance<ICmPossibilityListRepository>().GetObject(parentListGuid);
		}

		/// <summary>
		/// Set custom field data for one field (specified by owner HVO and field ID).
		/// </summary>
		/// <returns><c>true</c>, if custom field data was set, <c>false</c> otherwise (e.g., if value hadn't changed,
		/// or value was null, or field type was one not implemented in FDO, such as CellarPropertyType.Float).</returns>
		/// <param name="hvo">HVO of object whose field we're setting.</param>
		/// <param name="flid">Field ID of custom field to set.</param>
		/// <param name="value">Field's new value (as returned by GetCustomFieldData).</param>
		/// <param name="guidOrGuids">GUID or guids associated with new value (as returned by GetCustomFieldData).
		/// May be null or BsonNull.Value if no GUIDs associated with this value.</param>
		public bool SetCustomFieldData(int hvo, int flid, BsonValue value, BsonValue guidOrGuids)
		{
			if ((value == null) || (value == BsonNull.Value) ||
				((value.BsonType == BsonType.Array) && (value.AsBsonArray.Count == 0)))
				return false;
			List<Guid> fieldGuids = new List<Guid>();
			if (guidOrGuids == null || guidOrGuids == BsonNull.Value)
			{
				// Leave fieldGuids as an empty list
			}
			else
			{
				if (guidOrGuids is BsonArray)
					fieldGuids.AddRange(guidOrGuids.AsBsonArray.Select(bsonValue => ParseGuidOrDefault(bsonValue.AsString)));
				else
					fieldGuids.Add(ParseGuidOrDefault(guidOrGuids.AsString));
			}
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
//			logger.Debug("Custom field named {0} has type {1}", fieldName, fieldType.ToString());
			if (fieldName == null)
				return false;

			// Valid field types in FDO are GenDate, Integer, String, OwningAtomic, ReferenceAtomic, and ReferenceCollection, so that's all we implement.
			switch (fieldType)
			{
			case CellarPropertyType.GenDate:
				{
					var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					string valueAsString = valueAsMultiText.BestString(new string[] { MagicStrings.LanguageCodeForGenDateFields });
					if (string.IsNullOrEmpty(valueAsString))
						return false;
					GenDate valueAsGenDate;
					if (GenDate.TryParse(valueAsString, out valueAsGenDate))
					{
						GenDate oldValue = data.get_GenDateProp(hvo, flid);
						if (oldValue == valueAsGenDate)
							return false;
						else
						{
							data.SetGenDate(hvo, flid, valueAsGenDate);
							return true;
						}
					}
					return false;
				}

			case CellarPropertyType.Integer:
				{
					var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					string valueAsString = valueAsMultiText.BestString(new string[] { MagicStrings.LanguageCodeForIntFields });
					if (string.IsNullOrEmpty(valueAsString))
						return false;
					int valueAsInt;
					if (int.TryParse(valueAsString, out valueAsInt))
					{
						int oldValue = data.get_IntProp(hvo, flid);
						if (oldValue == valueAsInt)
							return false;
						else
						{
							data.SetInt(hvo, flid, valueAsInt);
							return true;
						}
					}
					return false;
				}

			case CellarPropertyType.OwningAtomic:
				{
					// Custom field is a MultiparagraphText, which is an IStText object in FDO
					IStTextRepository textRepo = servLoc.GetInstance<IStTextRepository>();
					Guid fieldGuid = fieldGuids.FirstOrDefault();
					IStText text;
					if (!textRepo.TryGetObject(fieldGuid, out text))
					{
						int currentFieldContentsHvo = data.get_ObjectProp(hvo, flid);
						if (currentFieldContentsHvo != FdoCache.kNullHvo)
							text = (IStText)cache.GetAtomicPropObject(currentFieldContentsHvo);
						else
						{
							// NOTE: I don't like the "magic" -2 number below, but FDO doesn't seem to have an enum for this. 2015-11 RM
							int newStTextHvo = data.MakeNewObject(cache.GetDestinationClass(flid), hvo, flid, -2);
							text = (IStText)cache.GetAtomicPropObject(newStTextHvo);
						}
					}
					// Shortcut: if text contents haven't changed, we don't want to change anything at all
					BsonValue currentFdoTextContents = ConvertUtilities.GetCustomStTextValues(text, flid,
						servLoc.WritingSystemManager, fdoMetaData, cache.DefaultUserWs);
					if ((currentFdoTextContents == BsonNull.Value || currentFdoTextContents == null) &&
						(value == BsonNull.Value || value == null))
						return false;
					if (currentFdoTextContents != null && currentFdoTextContents.Equals(value))
					{
						// No changes needed.
						return false;
					}
					// BsonDocument passed in contains "paragraphs". ParseCustomStTextValuesFromBson wants only a "value" element
					// inside the doc, so we'll need to construct a new doc for the StTextValues.
					BsonDocument doc = value.AsBsonDocument;
					LfMultiParagraph multiPara = BsonSerializer.Deserialize<LfMultiParagraph>(doc);
					// Now we have another way to check for "old value and new value were the same": if the FDO multiparagraph was empty,
					// GetCustomStTextValues will have returned null -- so if this multiPara has no paragraphs, that's also an unchanged situation
					if ((multiPara.Paragraphs == null || multiPara.Paragraphs.Count <= 0) &&
					    (currentFdoTextContents == BsonNull.Value || currentFdoTextContents == null))
						return false;
					int wsId;
					if (multiPara.InputSystem == null)
						wsId = fdoMetaData.GetFieldWs(flid);
					else
						wsId = servLoc.WritingSystemFactory.GetWsFromStr(multiPara.InputSystem);
					ConvertUtilities.SetCustomStTextValues(text, multiPara.Paragraphs, wsId);

					return true;
				}

			case CellarPropertyType.ReferenceAtomic:
				if (fieldGuids.FirstOrDefault() != Guid.Empty)
				{
					int referencedHvo = data.get_ObjFromGuid(fieldGuids.FirstOrDefault());
					int oldHvo = data.get_ObjectProp(hvo, flid);
					if (referencedHvo == oldHvo)
						return false;
					else
					{
						data.SetObjProp(hvo, flid, referencedHvo);
						// TODO: What if the value of the referenced object has changed in LanguageForge? (E.g., change that possibility's text from "foo" to "bar")
						// Need to implement that scenario.
						return true;
					}
				}
				else
				{
					// It's a reference to an ICmPossibility instance: create a new entry in appropriate PossibilityList
					LfStringField valueAsLfStringField = BsonSerializer.Deserialize<LfStringField>(value.AsBsonDocument);
					string nameHierarchy = valueAsLfStringField.Value;
					if (nameHierarchy == null)
						return false;
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					// Oddly, this can return 0 for some custom fields. TODO: Find out why: that seems like it would be an error.
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);
					ICmPossibility newPoss = parentList.FindOrCreatePossibility(nameHierarchy, fieldWs);

					int oldHvo = data.get_ObjectProp(hvo, flid);
					if (newPoss.Hvo == oldHvo)
						return false;
					else
					{
						data.SetObjProp(hvo, flid, newPoss.Hvo);
						return true;
					}
				}

			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				{
					if (value == null || value == BsonNull.Value)
					{
						// Can't write null to a collection or sequence in FDO; it's forbidden. So data.SetObjProp(hvo, flid, FdoCache.kNullHvo) will not work.
						// Instead, we delete all items from the existing collection or sequence, and thus store an empty coll/seq in FDO.
						int oldSize = data.get_VecSize(hvo, flid);
						if (oldSize == 0)
						{
							// It was already empty, so leave it unchanged so we don't cause unnecessary changes in the .fwdata XML (and unnecessary Mercurial commits).
							return false;
						}
						else
						{
							data.Replace(hvo, flid, 0, oldSize, null, 0); // This is how you set an empty array
							return true;
						}
					}
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					// TODO: Investigate why this is sometimes coming back as 0 instead of as a real writing system ID
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);

					LfStringArrayField valueAsStringArray = BsonSerializer.Deserialize<LfStringArrayField>(value.AsBsonDocument);

					// Step 1: Check if any of the fieldGuids is Guid.Empty, which would indicate a brand-new object that wasn't in FDO
					List<string> fieldData = valueAsStringArray.Values;
					while (fieldGuids.Count < valueAsStringArray.Values.Count)
					{
						fieldGuids.Add(Guid.Empty); // Ensure the Zip can run all the way through
					}
					IEnumerable<ICmPossibility> fieldObjs = fieldGuids.Zip<Guid, string, ICmPossibility>(fieldData, (thisGuid, thisData) =>
						{
							ICmPossibility newPoss;
							if (thisGuid == default(Guid)) {
								newPoss = ((ICmPossibilityList)parentList).FindOrCreatePossibility(thisData, fieldWs);
								// TODO: If this is a new possibility, then we need to populate it with ALL the corresponding data from LF,
								// which we don't necessarily have at this point. Need to make that a separate step in the Send/Receive.
								return newPoss;
							}
							else {
								newPoss = servLoc.GetInstance<ICmPossibilityRepository>().GetObject(thisGuid);
								return newPoss;
							}
						});

					// Step 2: Remove any objects from the "old" list that weren't in the "new" list
					// We have to look them up by HVO because that's the only public API available in FDO
					// Following logic inspired by XmlImportData.CopyCustomFieldData in FieldWorks source
					int[] oldHvosArray = data.VecProp(hvo, flid);
					int[] newHvosArray = fieldObjs.Select(poss => poss.Hvo).ToArray();
					// Shortcut check
					if (oldHvosArray.SequenceEqual(newHvosArray))
					{
						// Nothing to do, so return now so that we don't cause unnecessary changes and commits in Mercurial
						return false;
					}
					HashSet<int> newHvos = new HashSet<int>(newHvosArray);
					HashSet<int> combinedHvos = new HashSet<int>();
					// Loop backwards so deleting items won't mess up indices of subsequent deletions
					for (int idx = oldHvosArray.Length - 1; idx >= 0; idx--)
					{
						int oldHvo = oldHvosArray[idx];
						if (newHvos.Contains(oldHvo))
							combinedHvos.Add(oldHvo);
						else
							data.Replace(hvo, flid, idx, idx + 1, null, 0); // Important to pass *both* null *and* 0 here to remove items
					}

					// Step 3: Add any objects from the "new" list that weren't in the "old" list
					foreach (int newHvo in newHvosArray)
					{
						if (combinedHvos.Contains(newHvo))
							continue;
						// This item was added in the new list
						data.Replace(hvo, flid, combinedHvos.Count, combinedHvos.Count, new int[] { newHvo }, 1);
						combinedHvos.Add(newHvo);
					}
					return true;
				}

			case CellarPropertyType.String:
				{
					var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					int wsIdForField = fdoMetaData.GetFieldWs(flid);
					string wsStrForField = servLoc.WritingSystemFactory.GetStrFromWs(wsIdForField);
					KeyValuePair<string, string> kv = valueAsMultiText.BestStringAndWs(new string[] { wsStrForField });
					string foundWs = kv.Key ?? string.Empty;
					string foundData = kv.Value ?? string.Empty;
					int foundWsId = servLoc.WritingSystemFactory.GetWsFromStr(foundWs);
					if (foundWsId == 0)
						return false; // Skip any unidentified writing systems
					ITsString oldValue = data.get_StringProp(hvo, flid);
					ITsString newValue = ConvertMongoToFdoTsStrings.SpanStrToTsString(foundData, foundWsId, servLoc.WritingSystemFactory);
					if (oldValue != null && TsStringUtils.GetDiffsInTsStrings(oldValue, newValue) == null) // GetDiffsInTsStrings() returns null when there are no changes
						return false;
					else
					{
						data.SetString(hvo, flid, newValue);
						return true;
					}
				}

			default:
				return false;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
		}

		public void SetCustomFieldsForThisCmObject(ICmObject cmObj, string objectType, BsonDocument customFieldValues, BsonDocument customFieldGuids)
		{
			if (customFieldValues == null) return;

			IEnumerable<int> customFieldIds =
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid));

			var remainingFieldNames = new HashSet<string>(customFieldValues.Select(elem => elem.Name));
			foreach (int flid in customFieldIds)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return;
				fieldName = ConvertUtilities.NormalizedFieldName(fieldName, objectType);
				BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				BsonValue fieldGuidOrGuids = (customFieldGuids == null) ? BsonNull.Value : customFieldGuids.GetValue(fieldName, BsonNull.Value);
				// Persist Guid.Empty as null to save space
				if (fieldGuidOrGuids.BsonType == BsonType.String && fieldGuidOrGuids.AsString == "00000000-0000-0000-0000-000000000000")
					fieldGuidOrGuids = BsonNull.Value;
				remainingFieldNames.Remove(fieldName);
				if (fieldValue != BsonNull.Value)
				{
					SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				}
			}
			foreach (string fieldName in remainingFieldNames)
			{
				// TODO: These are NEW CUSTOM FIELDS! Will need to create them in FDO, then do:
				// BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				// BsonValue fieldGuidOrGuids = customFieldGuids.GetValue(fieldName, BsonNull.Value);
				// SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				// Above lines commented out until we can create new custom fields correctly. 2015-11 RM
				logger.Warning("Custom field {0} from LF skipped, because we're not yet creating new custom fields in FDO", fieldName);
			}
		}
	}
}