// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoCustomField
	{
		private FdoCache cache;
		private IFdoServiceLocator servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;
		private ILogger logger;

		public ConvertMongoToFdoCustomField(FdoCache cache, ILogger logger)
		{
			this.cache = cache;
			servLoc = cache.ServiceLocator;
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			this.logger = logger;
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result = default(Guid);
			Guid.TryParse(input, out result);
			return result;
		}

		private List<string> ParseCustomStTextValuesFromBson(BsonDocument source, out int wsId)
		{
			var result = new List<string>();
			wsId = 0;
			if (source.ElementCount <= 0)
				return result;
			LfMultiText valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(source);
			KeyValuePair<int, string> kv = valueAsMultiText.WsIdAndFirstNonEmptyString(cache);
			wsId = kv.Key;
			string htmlContents = kv.Value;
			result.AddRange(htmlContents.Split(new string[] { "</p>" }, StringSplitOptions.RemoveEmptyEntries)
				.Select(para => para.StartsWith("<p>") ? para.Substring(3) : para));
			// No need to trim trailing </p> as String.Split has already done that for us
			return result;
		}

		public ICmPossibilityList GetParentListForField(int flid)
		{
			Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
			if (parentListGuid == Guid.Empty)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				Console.WriteLine("No possibility list found for custom field {0}; giving up", fieldName);
				return null;
				// TODO: If this happens, we're probably importing a newly-created possibility list, so we should
				// probably create it in FDO. Implementation needed.
			}
			return (ICmPossibilityList)servLoc.GetObject(parentListGuid);
		}

		/// <summary>
		/// Set custom field data for one field (specified by owner HVO and field ID).
		/// </summary>
		/// <returns><c>true</c>, if custom field data was set, <c>false</c> otherwise
		/// (e.g., if value was null, or field type was one not implemented in FDO, such as CellarPropertyType.Float).</returns>
		/// <param name="hvo">HVO of object whose field we're setting.</param>
		/// <param name="flid">Field ID of custom field to set.</param>
		/// <param name="value">Field's new value (as returned by GetCustomFieldData).</param>
		/// <param name="guidOrGuids">GUID or guids associated with new value (as returned by GetCustomFieldData).
		/// May be null or BsonNull.Value if no GUIDs associated with this value.</param>
		public bool SetCustomFieldData(int hvo, int flid, BsonValue value, BsonValue guidOrGuids)
		{
			if (value == null || value == BsonNull.Value)
				return false;
			List<Guid> fieldGuids = new List<Guid>();
			if (guidOrGuids == null || guidOrGuids == BsonNull.Value)
			{
				fieldGuids.Add(Guid.Empty);
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
			logger.Debug("Custom field named {0} has type {1}", fieldName, fieldType.ToString());
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
						data.SetGenDate(hvo, flid, valueAsGenDate);
						return true;
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
						data.SetInt(hvo, flid, valueAsInt);
						return true;
					}
					return false;
				}

			case CellarPropertyType.OwningAtomic:
				{
					// Custom field is a MultiparagraphText, which is an IStText object in FDO
					IStTextRepository textRepo = cache.ServiceLocator.GetInstance<IStTextRepository>();
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
					BsonValue currentFdoTextContents = ConvertUtilities.GetCustomStTextValues(text, flid,
						cache.ServiceLocator.WritingSystemManager, cache.MetaDataCacheAccessor, cache.DefaultUserWs);
					if (currentFdoTextContents == null && value == null)
						return true;
					if (currentFdoTextContents != null && currentFdoTextContents.Equals(value))
					{
						// No changes needed.
						return true;
					}
					// TODO: In the future when we don't create a new object but re-use the existing one, we'll need
					// to compare paragraph contents and call newStText.AddNewTextPara() or newStText.DeleteParagraph() as many
					// times as needed to have the right # of paras. Then set the contents of each paragraph to their new values.
					//
					// For now, though, we just clear out all the current paragraphs, then add a number of paragraphs to the newly-empty object.
					int wsId;
					List<string> newParagraphs = ParseCustomStTextValuesFromBson(value.AsBsonDocument, out wsId);
					// Keep styles even though we're replacing contents. If we've added new paragraphs in LF, give them the same
					// style as the first paragraph. This should work most of the time.
					List<string> currentStyles = text.ParagraphsOS.Select(stPara => stPara.StyleName).ToList();
					if (currentStyles.Count > 0 && currentStyles.Count < newParagraphs.Count)
					{
						int countDifference = newParagraphs.Count - currentStyles.Count;
						currentStyles.AddRange(Enumerable.Repeat(currentStyles.First(), countDifference));
					}
					// Clear contents, keep styles
					text.ParagraphsOS.Clear();
					foreach (Tuple<string,string> textAndStyle in newParagraphs.Zip(currentStyles, (a,b) => new Tuple<string,string>(a,b)))
					{
						string paraContents = textAndStyle.Item1;
						string styleName = textAndStyle.Item2;
						IStTxtPara newPara = text.AddNewTextPara(styleName);
						newPara.Contents = TsStringUtils.MakeTss(paraContents, wsId);
					}
					return true;
				}

			case CellarPropertyType.ReferenceAtomic:
				Console.WriteLine("ReferenceAtomic field named {0} with value {1}", fieldName, value.ToJson());
				int log_fieldWs = fdoMetaData.GetFieldWs(flid);
				string log_fieldWsStr = servLoc.WritingSystemManager.GetStrFromWs(log_fieldWs);
				Console.WriteLine("Writing system for this field has ID {0} and name ({1})", log_fieldWs, log_fieldWsStr);
				if (fieldGuids.First() != Guid.Empty)
				{
					int referencedHvo = data.get_ObjFromGuid(fieldGuids.First());
					data.SetObjProp(hvo, flid, referencedHvo);
					// TODO: What if the value of the referenced object has changed in LanguageForge? (E.g., change that possibility's text from "foo" to "bar")
					// Need to implement that scenario.
					return true;
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

					data.SetObjProp(hvo, flid, newPoss.Hvo);
					return true;
				}

			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				{
					if (value == null || value == BsonNull.Value) return false;
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					// TODO: Investigate why this is sometimes coming back as 0 instead of as a real writing system ID
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);

					LfStringArrayField valueAsStringArray = BsonSerializer.Deserialize<LfStringArrayField>(value.AsBsonDocument);

					// Step 1: Check if any of the fieldGuids is Guid.Empty, which would indicate a brand-new object that wasn't in FDO
					List<string> fieldData = valueAsStringArray.Values;
					Console.WriteLine("Reference collection had values {0}", String.Join(", ", fieldData));
					Console.WriteLine("Reference collection had GUIDs {0}", guidOrGuids.ToJson());
					IEnumerable<ICmPossibility> fieldObjs = fieldGuids.Zip<Guid, string, ICmPossibility>(fieldData, (thisGuid, thisData) =>
						{
							ICmPossibility newPoss;
							if (thisGuid == default(Guid)) {
								newPoss = ((ICmPossibilityList)parentList).FindOrCreatePossibility(thisData, fieldWs);
								return newPoss;
							}
							else {
								newPoss = servLoc.GetObject(thisGuid) as ICmPossibility;
								return newPoss;
							}
						});

					// Step 2: Remove any objects from the "old" list that weren't in the "new" list
					// We have to look them up by HVO because that's the only public API available in FDO
					// Following logic inspired by XmlImportData.CopyCustomFieldData in FieldWorks source
					int[] oldHvosArray = data.VecProp(hvo, flid);
					int[] newHvosArray = fieldObjs.Select(poss => poss.Hvo).ToArray();
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
					int wsIdForField = cache.MetaDataCacheAccessor.GetFieldWs(flid);
					string wsStrForField = cache.WritingSystemFactory.GetStrFromWs(wsIdForField);
					KeyValuePair<string, string> kv = valueAsMultiText.BestStringAndWs(new string[] { wsStrForField });
					string foundWs = kv.Key;
					string foundData = kv.Value;
					int foundWsId = cache.WritingSystemFactory.GetWsFromStr(foundWs);
					data.SetString(hvo, flid, TsStringUtils.MakeTss(foundData, foundWsId));
					return true;
				}

			default:
				return false;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			// return false; // If compiler complains about unreachable code, GOOD! We got the switch statement right. Otherwise this is our catch-all.
		}

		public void SetCustomFieldsForThisCmObject(ICmObject cmObj, string objectType, BsonDocument customFieldValues, BsonDocument customFieldGuids)
		{
			if (customFieldValues == null) return;
			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

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
					Console.WriteLine("Setting custom field {0} with data {1} and GUID(s) {2}", fieldName, fieldValue.ToJson(), fieldGuidOrGuids.ToJson());
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
				Console.WriteLine("Custom field {0} skipped because we're not yet creating new custom fields in FDO", fieldName);
			}
		}
	}
}