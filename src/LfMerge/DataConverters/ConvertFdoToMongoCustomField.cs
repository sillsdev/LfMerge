// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Infrastructure;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoCustomField
	{
		private FdoCache cache;
		private IFwMetaDataCacheManaged fdoMetaData;
		private ILogger logger;

		/// <summary>
		/// Mapping of FDO CellarProperty type enumeration to LF custom field type.
		/// Refer to FDO SIL.CoreImpl.CellarPropertyType.
		/// </summary>
		private static readonly Dictionary<CellarPropertyType, string> CellarPropertyTypeToLfCustomFieldType = new Dictionary<CellarPropertyType, string>
		{
			{CellarPropertyType.ReferenceCollection, "Multi_ListRef"},
			{CellarPropertyType.String, "Single_Line"},
			{CellarPropertyType.MultiString, "MultiPara"},
			{CellarPropertyType.MultiUnicode, "MultiPara"},
			{CellarPropertyType.OwningAtom, "MultiPara"}, // Equivalent to MinObj
			{CellarPropertyType.ReferenceAtomic, "Single_ListRef"},

			// The following custom fields currently aren't displayed in LF
			//{CellarPropertyType.Integer, "Number"},
			//{CellarPropertyType.GenDate, "Date"},
		};

		private Dictionary<Guid, string> GuidToListCode;

		public ConvertFdoToMongoCustomField(FdoCache cache, ILogger logger)
		{
			this.cache = cache;
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			this.logger = logger;

			GuidToListCode = new Dictionary<Guid, string>
			{
				{cache.LanguageProject.LexDbOA.DomainTypesOA.Guid, MagicStrings.LfOptionListCodeForAcademicDomainTypes},
				{cache.LanguageProject.AnthroListOA.Guid, MagicStrings.LfOptionListCodeForAnthropologyCodes},
				{cache.LanguageProject.LexDbOA.PublicationTypesOA.Guid, MagicStrings.LfOptionListCodeForDoNotPublishIn},
				{cache.LanguageProject.PartsOfSpeechOA.Guid, MagicStrings.LfOptionListCodeForGrammaticalInfo},
				{cache.LanguageProject.LocationsOA.Guid, MagicStrings.LfOptionListCodeForLocations},
				{cache.LanguageProject.SemanticDomainListOA.Guid, MagicStrings.LfOptionListCodeForSemanticDomains},
				{cache.LanguageProject.LexDbOA.SenseTypesOA.Guid, MagicStrings.LfOptionListCodeForSenseTypes},
				{cache.LanguageProject.StatusOA.Guid, MagicStrings.LfOptionListCodeForStatus},
				{cache.LanguageProject.LexDbOA.UsageTypesOA.Guid, MagicStrings.LfOptionListCodeForUsageTypes}
			};
		}

		public bool CreateCustomFieldsConfigViews(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			return CreateCustomFieldsConfigViews(project, lfCustomFieldList, false);
		}

		public bool CreateCustomFieldsConfigViews(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList, bool isTest)
		{
			var customFieldSpecs = new List<CustomFieldSpec>();
			// TODO: fill in customFieldSpecs from lfCustomFieldList

			string className = "Api\\Model\\Languageforge\\Lexicon\\Command\\LexProjectCommands";
			string methodName = "updateCustomFieldViews";
			var parameters = new List<Object>();
			parameters.Add(project.ProjectCode);
			parameters.Add(customFieldSpecs);
			string output = PhpConnection.RunClass(className, methodName, parameters, isTest);

			if (string.IsNullOrEmpty(output) || output == "false")
				return false;
			return true;
		}

		/// <summary>
		/// Returns value of custom fields for this CmObject.
		/// </summary>
		/// <returns>Either null or a BsonDocument with the following structure
		///  <br />
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
		/// <param name="lfCustomFieldList">Dictionary to receive LF custom field configuration settings (keys are field names
		/// as found in LF, e.g. customField_entry_MyCustomField)</param>
		public BsonDocument GetCustomFieldsForThisCmObject(ICmObject cmObj, string objectType,
			Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			if (cmObj == null) return null;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var customFieldData = new BsonDocument();
			var customFieldGuids = new BsonDocument();

			foreach (int flid in customFieldIds)
			{
				string label = fdoMetaData.GetFieldNameOrNull(flid);
				if (label == null)
					return null;
				string lfCustomFieldName = ConvertUtilities.NormalizedFieldName(label, objectType);
				BsonDocument bsonForThisField;
				string lfCustomFieldType;
				GetCustomFieldData(cmObj.Hvo, flid, objectType,
					out bsonForThisField, out lfCustomFieldType);

				// Get custom field configuration info
				if (label.Contains("ListRef"))
				{
					// Compute list code
					string listCode = GetParentListCode(flid);
					lfCustomFieldList[lfCustomFieldName] = GetLfCustomFieldSettings(label, listCode);
				}
				else if (!string.IsNullOrEmpty(lfCustomFieldType))
				{
					// Default to current analysis WS?
					List<string>inputSystems = new List<string>();
					foreach (var fdoAnalysisWs in cache.LangProject.CurrentAnalysisWritingSystems)
						inputSystems.Add(fdoAnalysisWs.RFC5646);
					lfCustomFieldList[lfCustomFieldName] =
						GetLfCustomFieldSettings(label, lfCustomFieldType, inputSystems);
				}

				if (bsonForThisField != null)
				{
					customFieldData.Add(lfCustomFieldName, bsonForThisField["value"]);
					BsonValue guid;
					if (bsonForThisField.TryGetValue("guid", out guid))
					{
						customFieldGuids.Add(lfCustomFieldName, guid);

						LfConfigFieldBase lfCustomFieldSettings;
						// Valid guid so we should be able to create custom field configuration info
						if (lfCustomFieldName.Contains("ListRef"))
							lfCustomFieldSettings = GetLfCustomFieldSettings(lfCustomFieldName, "");
						else
							lfCustomFieldSettings = GetLfCustomFieldSettings(lfCustomFieldName,
								lfCustomFieldType, new List<string>{cache.LanguageProject.DefaultAnalysisWritingSystem.ToString()});

						lfCustomFieldList[lfCustomFieldName] = lfCustomFieldSettings;
					}
				}
			}

			BsonDocument result = new BsonDocument();
			result.Add("customFields", customFieldData);
			result.Add("customFieldGuids", customFieldGuids);
			return result;
		}


		/// <summary>
		/// Get the list code of the parent
		/// </summary>
		/// <returns>The list code as used in LF (e.g., "sense-type" or "grammatical-info").</returns>
		/// <param name="flid">Flid.</param>
		private string GetParentListCode(int flid)
		{
			string result = string.Empty;
			Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
			if (parentListGuid != Guid.Empty)
				GuidToListCode.TryGetValue(parentListGuid, out result);

			return result;
		}

		/// <summary>
		/// Gets the data for one custom field, and any relevant GUIDs.
		/// </summary>
		/// <param name="hvo">Hvo of object we're getting the field for.</param>
		/// <param name="flid">Flid for this field.</param>
		/// <param name="fieldSourceType">Either "entry", "senses" or "examples". Could also be "allomorphs", eventually.</param>
		/// <param name="bsonForThisField">Output of a BsonDocument with the following structure: <br />
		/// { fieldName: { "value": BsonValue, "guid": "some-guid-as-a-string" } } <br />
		/// -OR- <br />
		/// { fieldName: { "value": BsonValue, "guid": ["guid1", "guid2", "guid3"] } } <br />
		/// The format of the fieldName key will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// The type of the "guid" value (array or string) will determine whether there is a single GUID,
		/// or a list of GUIDs that happens to contain only one entry.
		/// If there is no "guid" key, that field has no need for a GUID. (E.g., a number).
		/// </param>
		/// <param name="customFieldType">output string of LF custom field type</param>
		private void GetCustomFieldData(int hvo, int flid, string fieldSourceType, out BsonDocument bsonForThisField, out string customFieldType)
		{
			bsonForThisField = null;
			customFieldType = string.Empty;
			BsonValue fieldValue = null;
			BsonValue fieldGuid = null; // Might be a single value, might be a list (as a BsonArray)
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
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
					fieldValue = LfMultiText.FromSingleITsString(iTsValue, cache.ServiceLocator.WritingSystemManager).AsBsonDocument();
				break;

			default:
				fieldValue = null;
				if (logger != null)
					logger.Warning("FDO CellarPropertyType.{0} not recognized for LF custom field", fdoFieldType.ToString());
				break;
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
				bsonForThisField = result;
			}
		}

		private BsonValue GetCustomStTextValues(IStText obj, int flid)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Select(para => para.Contents).ToList();
			List<string> htmlParas = paras.Where(para => para != null).Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			ILgWritingSystemFactory wsManager = cache.ServiceLocator.WritingSystemManager;
			int fieldWs = cache.MetaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(cache.DefaultUserWs); // TODO: Should that be DefaultAnalWs instead?
			return new BsonDocument(wsStr, new BsonDocument("value", new BsonString(String.Join("", htmlParas))));
		}

		private BsonValue GetCustomListValues(ICmPossibility obj, int flid)
		{
			if (obj == null) return null;
			// TODO: Consider using obj.NameHierarchyString instead of obj.Name.BestAnalysisVernacularAlternative.Text
			return new BsonString(obj.Name.BestAnalysisVernacularAlternative.Text);
		}

		private LfConfigMultiText GetLfCustomFieldSettings(string label, string lfCustomFieldType, List<string> inputSystems)
		{
			if (lfCustomFieldType == null)
				return null;
			return new LfConfigMultiText {
				Label = label,
				DisplayMultiline = !lfCustomFieldType.Contains("Single") && !label.Contains("Single"),
				Width = 20,
				InputSystems = inputSystems,
			};
		}

		/// <summary>
		/// Gets the lf custom field settings.
		/// </summary>
		/// <returns>The lf custom field settings as LfConfigMultiOptionList or LfConfigOptionList.</returns>
		/// <param name="label">Custom field label.</param>
		/// <param name="listCode">Parent list code.</param>
		private LfConfigFieldBase GetLfCustomFieldSettings(string label, string listCode)
		{
			if (label.Contains("Multi ListRef"))
			{
				return new LfConfigMultiOptionList {
					Label = label,
					ListCode = listCode,
				};
			}
			else
			{
				return new LfConfigOptionList {
					Label = label,
					ListCode = listCode,
				};
			}
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
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			if (hvo == 0 || !data.get_IsValidObject(hvo))
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			ICmObject referencedObject = cache.GetAtomicPropObject(hvo);
			if (referencedObject == null)
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			referencedObjectGuids.Add(referencedObject.Guid);
			if (referencedObject is IStText)
				return ConvertUtilities.GetCustomStTextValues((IStText)referencedObject, flid,
					cache.ServiceLocator.WritingSystemManager, cache.MetaDataCacheAccessor, cache.DefaultUserWs);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
		}
	}
}

