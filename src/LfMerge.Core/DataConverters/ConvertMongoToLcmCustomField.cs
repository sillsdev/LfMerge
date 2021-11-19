// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.DomainServices;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToLcmCustomField
	{
		private LcmCache cache;
		private FwServiceLocatorCache servLoc;
		private IFwMetaDataCacheManaged lcmMetaData;
		private ILogger logger;
		private int wsEn;

		public ConvertMongoToLcmCustomField(LcmCache cache, FwServiceLocatorCache serviceLocator, ILogger logger, int wsEn)
		{
			this.cache = cache;
			this.servLoc = serviceLocator;
			this.lcmMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			this.logger = logger;
			this.wsEn = wsEn;
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result;
			return Guid.TryParse(input, out result) ? result : default(Guid);
		}

		public ICmPossibilityList GetParentListForField(int flid)
		{
			Guid parentListGuid = lcmMetaData.GetFieldListRoot(flid);
			if (parentListGuid == Guid.Empty)
			{
				string fieldName = lcmMetaData.GetFieldNameOrNull(flid);
				logger.Warning("No possibility list found for custom field {0} (field ID {1}); it will not be present in LCM", fieldName ?? "(name not found)", flid);
				return null;
				// TODO: If this happens, we're probably importing a newly-created possibility list, so we should
				// probably create it in LCM using ConvertMongoToLcmOptionList. Implementation needed.
			}
			return servLoc.GetInstance<ICmPossibilityListRepository>().GetObject(parentListGuid);
		}

		/// <summary>
		/// Set custom field data for one field (specified by owner HVO and field ID).
		/// </summary>
		/// <returns><c>true</c>, if custom field data was set, <c>false</c> otherwise (e.g., if value hadn't changed,
		/// or value was null, or field type was one not implemented in LCM, such as CellarPropertyType.Float).</returns>
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
			CellarPropertyType fieldType = (CellarPropertyType)lcmMetaData.GetFieldType(flid);
			string fieldName = lcmMetaData.GetFieldNameOrNull(flid);
//			logger.Debug("Custom field named {0} has type {1}", fieldName, fieldType.ToString());
			if (fieldName == null)
				return false;

			// Valid field types in LCM are GenDate, Integer, String, OwningAtomic, ReferenceAtomic, and ReferenceCollection, so that's all we implement.
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
					// Custom field is a MultiparagraphText, which is an IStText object in LCM
					IStTextRepository textRepo = servLoc.GetInstance<IStTextRepository>();
					Guid fieldGuid = fieldGuids.FirstOrDefault();
					IStText text;
					if (!textRepo.TryGetObject(fieldGuid, out text))
					{
						int currentFieldContentsHvo = data.get_ObjectProp(hvo, flid);
						if (currentFieldContentsHvo != LcmCache.kNullHvo)
							text = (IStText)cache.GetAtomicPropObject(currentFieldContentsHvo);
						else
						{
							// NOTE: I don't like the "magic" -2 number below, but LCM doesn't seem to have an enum for this. 2015-11 RM
							int newStTextHvo = data.MakeNewObject(cache.GetDestinationClass(flid), hvo, flid, -2);
							text = (IStText)cache.GetAtomicPropObject(newStTextHvo);
						}
					}
					// Shortcut: if text contents haven't changed, we don't want to change anything at all
					BsonValue currentLcmTextContents = ConvertUtilities.GetCustomStTextValues(text, flid,
						servLoc.WritingSystemManager, lcmMetaData, cache.DefaultUserWs);
					if ((currentLcmTextContents == BsonNull.Value || currentLcmTextContents == null) &&
						(value == BsonNull.Value || value == null))
						return false;
					if (currentLcmTextContents != null && currentLcmTextContents.Equals(value))
					{
						// No changes needed.
						return false;
					}
					// BsonDocument passed in contains "paragraphs". ParseCustomStTextValuesFromBson wants only a "value" element
					// inside the doc, so we'll need to construct a new doc for the StTextValues.
					BsonDocument doc = value.AsBsonDocument;
					LfMultiParagraph multiPara = BsonSerializer.Deserialize<LfMultiParagraph>(doc);
					// Now we have another way to check for "old value and new value were the same": if the LCM multiparagraph was empty,
					// GetCustomStTextValues will have returned null -- so if this multiPara has no paragraphs, that's also an unchanged situation
					if ((multiPara.Paragraphs == null || multiPara.Paragraphs.Count <= 0) &&
					    (currentLcmTextContents == BsonNull.Value || currentLcmTextContents == null))
						return false;
					int wsId;
					if (multiPara.InputSystem == null)
						wsId = lcmMetaData.GetFieldWs(flid);
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
					int fieldWs = lcmMetaData.GetFieldWs(flid);
					// Oddly, this can return 0 for some custom fields. TODO: Find out why: that seems like it would be an error.
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs; // TODO: Investigate, because this should probably be wsEn instead so that we can create correct keys.
					if (fieldWs < 0)
					{
						// FindOrCreatePossibility has a bug where it doesn't handle "magic" writing systems (e.g., -1 for default analysis, etc) and
						// throws an exception instead. So we need to get a real ws here.
						fieldWs = WritingSystemServices.ActualWs(cache, fieldWs, hvo, flid);
					}
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
						// Can't write null to a collection or sequence in LCM; it's forbidden. So data.SetObjProp(hvo, flid, LcmCache.kNullHvo) will not work.
						// Instead, we delete all items from the existing collection or sequence, and thus store an empty coll/seq in LCM.
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
					int fieldWs = lcmMetaData.GetFieldWs(flid);
					// TODO: Investigate why this is sometimes coming back as 0 instead of as a real writing system ID
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);

					LfStringArrayField valueAsStringArray = BsonSerializer.Deserialize<LfStringArrayField>(value.AsBsonDocument);

					// Step 1: Get ICmPossibility instances from the string keys that LF gave us

					// First go through all the GUIDs we have and match them up to the keys. If they match up,
					// then remove the keys from the list. Any remaining keys get looked up with FindOrCreatePossibility(), so now we
					// have a complete set of GUIDs (or ICmPossibility objects, which works out to the same thing).

					// TODO: This is all kind of ugly, and WAY too long for one screen. I could put it in its own function,
					// but there's really no real gain from that, as it simply moves the logic even further away from where
					// it needs to be. There's not really a *good* way to achieve simplicity with this code design, unfortunately.
					// The only thing that would be close to simple would be to call some functions from the LcmToMongo option list
					// converters, and that's pulling in code from the "wrong" direction, which has its own ugliness. Ugh.

					HashSet<string> keysFromLF = new HashSet<string>(valueAsStringArray.Values);
					var fieldObjs = new List<ICmPossibility>();
					foreach (Guid guid in fieldGuids)
					{
						ICmPossibility poss;
						string key = "";
						if (guid != default(Guid)) {
							poss = servLoc.GetInstance<ICmPossibilityRepository>().GetObject(guid);
							if (poss == null)
							{
								// TODO: Decide what to do with possibilities deleted from LCM
								key = "";
							}
							else
							{
								if (poss.Abbreviation == null)
								{
									key = "";
								}
								else
								{
									ITsString keyTss = poss.Abbreviation.get_String(wsEn);
									key = keyTss == null ? "" : keyTss.Text ?? "";
								}
								fieldObjs.Add(poss);
							}
						}
						keysFromLF.Remove(key);
						// Ignoring return value (HashSet.Remove returns false if the key wasn't present), because false could mean one of two things:
						// 1. The CmPossibility had its English abbreviation changed in LCM, but LF doesn't know this yet.
						//    If this is the case, the LF key won't match, but the GUID will still match. So we might end up creating
						//    duplicate entries below with the FindOrCreatePossibility. TODO: Need to verify that LCM->LF possibility lists
						//    get updated correctly if renames happen! (... Or use the OptionList converters, that's what they were for.)
						// 2. The CmPossibility was just created in LF and isn't in LCM yet. In which case we should have been using the
						//    OptionList converters, which would hopefully have handled creating the ICmPossibility instane in LCM.
						// Either way, we can't really use that fact later, since we can't be certain if the possibility was renamed or created.
					}
					// Any remaining keysFromLF strings did not have corresponding GUIDs in Mongo.
					// This is most likely because they were added by LF, which doesn't write to the customFieldGuids field.
					// So we assume they exist in FW, and just look them up.
					foreach (string key in keysFromLF)
					{
						ICmPossibility poss = parentList.FindOrCreatePossibility(key, wsEn);
						// TODO: If this is a new possibility, then we need to populate it with ALL the corresponding data from LF,
						// which we don't necessarily have at this point. Need to make that a separate step in the Send/Receive: converting option lists first.
						fieldObjs.Add(poss);
					}
					// logger.Debug("Custom field {0} for CmObject {1}: BSON list was [{2}] and customFieldGuids was [{3}]. This was translated to keysFromLF = [{4}] and fieldObjs = [{5}]",
					// 	fieldName,
					// 	hvo,
					// 	String.Join(", ", valueAsStringArray.Values),
					// 	String.Join(", ", fieldGuids.Select(g => g.ToString())),
					// 	String.Join(", ", keysFromLF.AsEnumerable()),
					// 	String.Join(", ", fieldObjs.Select(poss => poss.AbbrAndName))
					// );

					// We have to replace objects by HVO because that's the only public API available in LCM
					int[] oldHvosArray = data.VecProp(hvo, flid);
					int[] newHvosArray = fieldObjs.Select(poss => poss.Hvo).ToArray();
					return ConvertUtilities.ReplaceHvosInCustomField(hvo, flid, data, oldHvosArray, newHvosArray);
				}

			case CellarPropertyType.String:
				{
					var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					int wsIdForField = lcmMetaData.GetFieldWs(flid);
					string wsStrForField = servLoc.WritingSystemFactory.GetStrFromWs(wsIdForField);
					KeyValuePair<string, string> kv = valueAsMultiText.BestStringAndWs(new string[] { wsStrForField });
					string foundWs = kv.Key ?? string.Empty;
					string foundData = kv.Value ?? string.Empty;
					int foundWsId = servLoc.WritingSystemFactory.GetWsFromStr(foundWs);
					if (foundWsId == 0)
						return false; // Skip any unidentified writing systems
					ITsString oldValue = data.get_StringProp(hvo, flid);
					ITsString newValue = ConvertMongoToLcmTsStrings.SpanStrToTsString(foundData, foundWsId, servLoc.WritingSystemFactory);
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
				lcmMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid));

			var remainingFieldNames = new HashSet<string>(customFieldValues.Select(elem => elem.Name));
			foreach (int flid in customFieldIds)
			{
				string fieldName = lcmMetaData.GetFieldNameOrNull(flid);
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
				// TODO: These are NEW CUSTOM FIELDS! Will need to create them in LCM, then do:
				// BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				// BsonValue fieldGuidOrGuids = customFieldGuids.GetValue(fieldName, BsonNull.Value);
				// SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				// Above lines commented out until we can create new custom fields correctly. 2015-11 RM
				logger.Warning("Custom field {0} from LF skipped, because we're not yet creating new custom fields in LCM", fieldName);
			}
		}
	}
}