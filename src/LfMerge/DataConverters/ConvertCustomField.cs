// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public class ConvertCustomField
	{
		private FdoCache _cache;
		private IFdoServiceLocator _servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;

		/// <summary>
		/// Mapping of FDO CellarProperty type enumeration to LF custom field type.
		/// Refer to FDO SIL.CoreImpl.CellarPropertyType.
		/// </summary>
		private static readonly Dictionary<CellarPropertyType, string> CellarPropertyTypeToLfCustomFieldType = new Dictionary<CellarPropertyType, string>
		{
			{CellarPropertyType.ReferenceCollection, "Multi_ListRef"},
			{CellarPropertyType.GenDate, "Date"},
			{CellarPropertyType.OwningAtom, "MultiPara"}, // Equivalent to MinObj
			{CellarPropertyType.ReferenceAtomic, "Single_ListRef"}
		};

		public ConvertCustomField(FdoCache cache)
		{
			this._cache = cache;
			_servLoc = cache.ServiceLocator;
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
		}

		/// <summary>
		/// Returns value of custom fields for this CmObject.
		/// </summary>
		/// <returns>Either null, or a BsonDocument with the following structure: <br />
		/// { <br />
		///     "customFields": { fieldName: fieldValue, fieldName2: fieldValue2, etc. } <br />
		///     "customFieldGuids": { fieldName: "Guid-as-string", fieldName2: "Guid2-as-string", etc. } <br />
		/// } <br />
		/// -OR- <br />
		/// { <br />
		///     "customFields": { fieldName: fieldValue, fieldName2: fieldValue2, etc. } <br />
		///     "customFieldGuids": { fieldName: ["guid1", "guid2", "guid3"], fieldName2: "Guid2-as-string", etc. } <br />
		/// } <br />
		/// The format of the fieldName keys will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// Some fields have no need for a GUID (e.g., a custom number field), so not all fieldNames will appear in customFieldGuids.
		/// Only custom fields with actual data will be returned: empty lists and strings will be suppressed, and integer
		/// fields whose value is 0 will be suppressed. (They will get the default int value, 0, when read from Mongo, so this
		/// allows us to save space in the Mongo DB). If a custom field's value is suppressed, it will not appear in the output,
		/// and will not have a corresponding value in customFieldGuids.
		/// If ALL custom fields are suppressed because of having null, default or empty values, then this function will return
		/// null instead of returning a useless-but-not-actually-empty BsonDocument.
		/// </returns>
		/// <param name="cmObj">Cm object.</param>
		/// <param name="objectType">Either "entry", "senses", or "examples"</param>
		/// <param name="lfCustomFields">Output BSON document of the custom fields</param>
		/// <param name="lfCustomFieldType">String of the LF custom field type</param>
		/// <param name="lfCustomFieldSettings">Lf Config settings for the particular custom field</param>
		public void getCustomFieldsForThisCmObject(ICmObject cmObj, string objectType,
			out BsonDocument lfCustomFields, out string lfCustomFieldType, out LfConfigFieldBase lfCustomFieldSettings)
		{
			lfCustomFields = null;
			lfCustomFieldType = string.Empty;
			lfCustomFieldSettings = null;

			if (cmObj == null)
				return;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => _cache.GetIsCustomField(flid)));

			var customFieldData = new BsonDocument();
			var customFieldGuids = new BsonDocument();

			foreach (int flid in customFieldIds)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return;
				fieldName = NormalizedFieldName(fieldName, objectType);
				//string lfCustomFieldType;
				BsonDocument bsonForThisField;
				GetCustomFieldData(cmObj.Hvo, flid, objectType, out bsonForThisField, out lfCustomFieldType);
				if (bsonForThisField != null)
				{
					customFieldData.Add(fieldName, bsonForThisField["value"]);
					BsonValue guid;
					if (bsonForThisField.TryGetValue("guid", out guid))
					{
						customFieldGuids.Add(fieldName, guid);

						// Valid guid so we should be able to create custom field configuration info
						if (fieldName.Contains("ListRef"))
							lfCustomFieldSettings = GetLfCustomFieldSettings(fieldName, "");
						else
							lfCustomFieldSettings = GetLfCustomFieldSettings(fieldName, lfCustomFieldType, new List<string>{_cache.LanguageProject.DefaultAnalysisWritingSystem.ToString()});
					}
				}
			}

			BsonDocument result = new BsonDocument();
			result.Add("customFields", customFieldData);
			result.Add("customFieldGuids", customFieldGuids);
			lfCustomFields = result;
		}

		private string NormalizedFieldName(string fieldName, string fieldSourceType)
		{
			fieldName = fieldName.Replace(' ', '_');
			return String.Format("customField_{0}_{1}", fieldSourceType, fieldName);
		}

		/// <summary>
		/// Gets the data for one custom field, and any relevant GUIDs.
		/// </summary>
		/// <param name="hvo">Hvo of object we're getting the field for.</param>
		/// <param name="flid">Flid for this field.</param>
		/// <param name="fieldSourceType">Either "entry", "senses" or "examples". Could also be "allomorphs", eventually.</param>
		/// <param name="customFieldData">Output - A BsonDocument with the following structure: <br />
		/// { fieldName: { "value": BsonValue, "guid": "some-guid-as-a-string" } } <br />
		/// -OR- <br />
		/// { fieldName: { "value": BsonValue, "guid": ["guid1", "guid2", "guid3"] } } <br />
		/// The format of the fieldName key will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// The type of the "guid" value (array or string) will determine whether there is a single GUID,
		/// or a list of GUIDs that happens to contain only one entry.
		/// If there is no "guid" key, that field has no need for a GUID. (E.g., a number).
		/// </param>
		/// <param name="customFieldType">Output -Type of lf custom field ["Multi_ListRef", "Date", "MultiPara", "Single_ListRef"].</param>
		private void GetCustomFieldData(int hvo, int flid, string fieldSourceType, out BsonDocument customFieldData, out string customFieldType )
		{
			customFieldData = null;
			BsonValue fieldValue = null;
			BsonValue fieldGuid = null; // Might be a single value, might be a list (as a BsonArray)
			ISilDataAccessManaged data = (ISilDataAccessManaged)_cache.DomainDataByFlid;
			CellarPropertyType fdoFieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			var dataGuids = new List<Guid>();

			// Valid field types in FDO are GenDate, Integer, String, OwningAtomic, ReferenceAtomic, and ReferenceCollection, so that's all we implement.
			switch (fdoFieldType)
			{
			case CellarPropertyType.GenDate:
				GenDate genDate = data.get_GenDateProp(hvo, flid);
				string genDateStr = genDate.ToLongString();
				// LF wants single-string fields in the format { "ws": { "value": "contents" } }
				fieldValue = String.IsNullOrEmpty(genDateStr) ? null :
					LfMultiText.FromSingleStringMapping(
						MagicStrings.LanguageCodeForGenDateFields, genDateStr).AsBsonDocument();
				break;
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Integer:
				fieldValue = new BsonInt32(data.get_IntProp(hvo, flid));
				if (fieldValue.AsInt32 == default(Int32))
					fieldValue = null; // Suppress int fields with 0 in them, to save Mongo DB space
				else
					// LF wants single-string fields in the format { "ws": { "value": "contents" } }
					// LfMultiText.FromSingleStringMapping
					fieldValue = LfMultiText.FromSingleStringMapping(
						MagicStrings.LanguageCodeForIntFields, fieldValue.AsInt32.ToString()).AsBsonDocument();
				break;

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				fieldValue = GetCustomReferencedObject(ownedHvo, flid, ref dataGuids);
				if (fieldValue != null && fdoFieldType == CellarPropertyType.ReferenceAtomic)
				{
					// Single CmPossiblity reference - LF expects format like { "value": "name of possibility" }
					fieldValue = new BsonDocument("value", fieldValue);
				}
				fieldGuid = new BsonString(dataGuids.FirstOrDefault().ToString());
				break;

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				var innerValues = new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid, ref dataGuids)).Where(x => x != null));
				if (innerValues.Count == 0)
					fieldValue = null;
				else
				{
					fieldValue = new BsonDocument("values", innerValues);
					fieldGuid = new BsonArray(dataGuids.Select(guid => guid.ToString()));
				}
				break;

			case CellarPropertyType.String:
				ITsString iTsValue = data.get_StringProp(hvo, flid);
				if (iTsValue == null || String.IsNullOrEmpty(iTsValue.Text))
					fieldValue = null;
				else
					fieldValue = LfMultiText.FromSingleITsString(iTsValue, _cache.ServiceLocator.WritingSystemManager).AsBsonDocument();
				break;

			default:
				fieldValue = null;
				break;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			CellarPropertyTypeToLfCustomFieldType.TryGetValue(fdoFieldType, out customFieldType);
			if (fieldValue == null)
				return;
			else
			{
				var result = new BsonDocument();
				result.Add("value", fieldValue ?? BsonNull.Value); // BsonValues aren't allowed to have C# nulls; they have their own null representation
				if (fieldGuid is BsonArray)
					result.Add("guid", fieldGuid, ((BsonArray)fieldGuid).Count > 0);
				else
					result.Add("guid", fieldGuid, fieldGuid != null);
				customFieldData = result;
			}
		}

		private BsonValue GetCustomStTextValues(IStText obj, int flid)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Select(para => para.Contents).ToList();
			List<string> htmlParas = paras.Where(para => para != null).Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			WritingSystemManager wsManager = _cache.ServiceLocator.WritingSystemManager;
			int fieldWs = _cache.MetaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(_cache.DefaultUserWs); // TODO: Should that be DefaultAnalWs instead?
			return new BsonDocument(wsStr, new BsonDocument("value", new BsonString(String.Join("", htmlParas))));
		}

		private BsonValue GetCustomListValues(ICmPossibility obj, int flid)
		{
			if (obj == null) return null;
			// TODO: Consider using obj.NameHierarchyString instead of obj.Name.BestAnalysisVernacularAlternative.Text
			return new BsonString(obj.Name.BestAnalysisVernacularAlternative.Text);
		}

		private List<string> ParseCustomStTextValuesFromBson(BsonDocument source, out int wsId)
		{
			var result = new List<string>();
			wsId = 0;
			if (source.ElementCount <= 0)
				return result;
			LfMultiText valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(source);
			KeyValuePair<int, string> kv = valueAsMultiText.WsIdAndFirstNonEmptyString(_cache);
			wsId = kv.Key;
			string htmlContents = kv.Value;
			result.AddRange(htmlContents.Split(new string[] { "</p>" }, StringSplitOptions.RemoveEmptyEntries)
				.Select(para => para.StartsWith("<p>") ? para.Substring(3) : para));
			// No need to trim trailing </p> as String.Split has already done that for us
			return result;
		}

		private LfConfigMultiText GetLfCustomFieldSettings(string fieldName, string lfCustomFieldType, List<string> inputSystems)
		{
			LfConfigMultiText result = new LfConfigMultiText {
				Label = fieldName,
				DisplayMultiline = lfCustomFieldType.Contains("Single"),
				Width = 20,
				InputSystems = inputSystems,
			};
			return result;
		}

		private LfConfigOptionList GetLfCustomFieldSettings(string fieldName, string listCode = "")
		{
			LfConfigOptionList result = new LfConfigOptionList {
				Label = fieldName,
				ListCode = listCode
			};
			return result;
		}

		/// <summary>
		/// Get a BsonValue and GUID for the object referenced by a Reference or Owning field.
		/// The GUID will be returned by adding it to a list passed in by reference, so that
		/// ReferenceCollection, OwningSequence, and similar fields will be easy to process.
		/// Note that we guarantee that a GUID will always be added to the list, even if this function
		/// returns null for the object's data. That way Select(hvo => GetCustomReferencedObject(hvo, flid, ref myGuidList))
		/// will always return the same number of items as the number of GUIDs in myGuidList.
		/// </summary>
		/// <returns>The custom referenced object's data converted to a BsonValue.</returns>
		/// <param name="hvo">Hvo of referenced object.</param>
		/// <param name="flid">Flid of referring field (required to get correct writing system for an StText).</param>
		/// <param name="referencedObjectGuids">List to which referenced object's GUID will be added.</param>
		private BsonValue GetCustomReferencedObject(int hvo, int flid, ref List<Guid> referencedObjectGuids)
		{
			ISilDataAccessManaged data = (ISilDataAccessManaged)_cache.DomainDataByFlid;
			if (hvo == 0 || !data.get_IsValidObject(hvo))
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			ICmObject referencedObject = _cache.GetAtomicPropObject(hvo);
			if (referencedObject == null)
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			referencedObjectGuids.Add(referencedObject.Guid);
			if (referencedObject is IStText)
				return GetCustomStTextValues((IStText)referencedObject, flid);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result = default(Guid);
			Guid.TryParse(input, out result);
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
			return (ICmPossibilityList)_servLoc.GetObject(parentListGuid);
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
			ISilDataAccessManaged data = (ISilDataAccessManaged)_cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
			Console.WriteLine("Field named {0} has type {1}", fieldName, fieldType.ToString());
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
					IStTextRepository textRepo = _cache.ServiceLocator.GetInstance<IStTextRepository>();
					Guid fieldGuid = fieldGuids.FirstOrDefault();
					IStText text;
					if (!textRepo.TryGetObject(fieldGuid, out text))
					{
						int currentFieldContentsHvo = data.get_ObjectProp(hvo, flid);
						if (currentFieldContentsHvo != FdoCache.kNullHvo)
							text = (IStText)_cache.GetAtomicPropObject(currentFieldContentsHvo);
						else
						{
							// NOTE: I don't like the "magic" -2 number below, but FDO doesn't seem to have an enum for this. 2015-11 RM
							int newStTextHvo = data.MakeNewObject(_cache.GetDestinationClass(flid), hvo, flid, -2);
							text = (IStText)_cache.GetAtomicPropObject(newStTextHvo);
						}
					}
					BsonValue currentFdoTextContents = GetCustomStTextValues(text, flid);
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
				string log_fieldWsStr = _servLoc.WritingSystemManager.GetStrFromWs(log_fieldWs);
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
						fieldWs = _cache.DefaultUserWs;
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
						fieldWs = _cache.DefaultUserWs;
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
							newPoss = _servLoc.GetObject(thisGuid) as ICmPossibility;
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
					int wsIdForField = _cache.MetaDataCacheAccessor.GetFieldWs(flid);
					string wsStrForField = _cache.WritingSystemFactory.GetStrFromWs(wsIdForField);
					KeyValuePair<string, string> kv = valueAsMultiText.BestStringAndWs(new string[] { wsStrForField });
					string foundWs = kv.Key;
					string foundData = kv.Value;
					int foundWsId = _cache.WritingSystemFactory.GetWsFromStr(foundWs);
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
				.Where(flid => _cache.GetIsCustomField(flid)));

			var remainingFieldNames = new HashSet<string>(customFieldValues.Select(elem => elem.Name));
			foreach (int flid in customFieldIds)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return;
				fieldName = NormalizedFieldName(fieldName, objectType);
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

