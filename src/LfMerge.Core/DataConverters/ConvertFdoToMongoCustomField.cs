// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Infrastructure;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using MongoDB.Bson;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Core.DataConverters
{
	public class ConvertFdoToMongoCustomField
	{
		private FdoCache cache;
		private FwServiceLocatorCache servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;
		private ILogger logger;

		private int _wsEn;

		/// <summary>
		/// Mapping of FDO CellarProperty type enumeration to LF custom field type.
		/// Refer to FDO SIL.CoreImpl.CellarPropertyType.
		/// </summary>
		private static readonly Dictionary<CellarPropertyType, string> CellarPropertyTypeToLfCustomFieldType = new Dictionary<CellarPropertyType, string>
		{
			{CellarPropertyType.ReferenceCollection, "Multi_ListRef"},
			{CellarPropertyType.String, "Single_Line"},
			{CellarPropertyType.MultiString, "MultiText"},
			{CellarPropertyType.MultiUnicode, "MultiText"},
			{CellarPropertyType.OwningAtomic, "MultiParagraph"}, // Equivalent to MinObj
			{CellarPropertyType.ReferenceAtomic, "Single_ListRef"},

			// The following custom fields currently aren't displayed in LF
			//{CellarPropertyType.Integer, "Number"},
			//{CellarPropertyType.GenDate, "Date"},
		};

		private Dictionary<Guid, string> GuidToListCode;

		public ConvertFdoToMongoCustomField(FdoCache cache, FwServiceLocatorCache serviceLocator, ILogger logger)
		{
			this.cache = cache;
			this.servLoc = serviceLocator;
			this.fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			this.logger = logger;
			_wsEn = servLoc.WritingSystemFactory.GetWsFromStr("en");

			GuidToListCode = new Dictionary<Guid, string>
			{
				{servLoc.LanguageProject.LexDbOA.DomainTypesOA.Guid, MagicStrings.LfOptionListCodeForAcademicDomainTypes},
				{servLoc.LanguageProject.AnthroListOA.Guid, MagicStrings.LfOptionListCodeForAnthropologyCodes},
				{servLoc.LanguageProject.PartsOfSpeechOA.Guid, MagicStrings.LfOptionListCodeForGrammaticalInfo},
				{servLoc.LanguageProject.LocationsOA.Guid, MagicStrings.LfOptionListCodeForLocations},
				{servLoc.LanguageProject.SemanticDomainListOA.Guid, MagicStrings.LfOptionListCodeForSemanticDomains},
				{servLoc.LanguageProject.LexDbOA.SenseTypesOA.Guid, MagicStrings.LfOptionListCodeForSenseTypes},
				{servLoc.LanguageProject.StatusOA.Guid, MagicStrings.LfOptionListCodeForStatus},
				{servLoc.LanguageProject.LexDbOA.UsageTypesOA.Guid, MagicStrings.LfOptionListCodeForUsageTypes}
			};
		}

		public bool CreateCustomFieldsConfigViews(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList, Dictionary<string, string> lfCustomFieldTypes)
		{
			return CreateCustomFieldsConfigViews(project, lfCustomFieldList, lfCustomFieldTypes, false);
		}

		// TODO: Get rid of isTest bool
		public bool CreateCustomFieldsConfigViews(ILfProject project, Dictionary<string, LfConfigFieldBase> lfCustomFieldList, Dictionary<string, string> lfCustomFieldTypes, bool isTest)
		{
			var customFieldSpecs = new List<CustomFieldSpec>();
			foreach (string lfCustomFieldName in lfCustomFieldList.Keys)
			{
				customFieldSpecs.Add(new CustomFieldSpec(lfCustomFieldName, lfCustomFieldTypes[lfCustomFieldName]));
			}
			// TODO: This no longer needs to return a bool
			return true;
		}

		/// <summary>
		/// Returns a dictionary of custom fields at the LexEntry, LexSense, and LexExampleSentence levels
		/// From FDO to LF.  If the dictionary doesn't exist, create one.
		/// </summary>
		/// <returns>Dictionary of custom fields where the keys are the parent listCode</returns>
		public Dictionary<string, ICmPossibilityList> GetCustomFieldParentLists()
		{
			// Generate the dictionary of custom fields
			var lfCustomFieldLists = new Dictionary<string, ICmPossibilityList>();

			// The three classes that are allowed to have custom fields in them are LexEntry, LexSense, and LexExampleSentence
			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(LexEntryTags.kClassId, false, (int)CellarPropertyTypeFilter.AllReference)
				.Where(flid => cache.GetIsCustomField(flid) && fdoMetaData.GetFieldListRoot(flid) != Guid.Empty));
			customFieldIds.AddRange(
				fdoMetaData.GetFields(LexSenseTags.kClassId, false, (int)CellarPropertyTypeFilter.AllReference)
				.Where(flid => cache.GetIsCustomField(flid) && fdoMetaData.GetFieldListRoot(flid) != Guid.Empty));
			customFieldIds.AddRange(
				fdoMetaData.GetFields(LexExampleSentenceTags.kClassId, false, (int)CellarPropertyTypeFilter.AllReference)
				.Where(flid => cache.GetIsCustomField(flid) && fdoMetaData.GetFieldListRoot(flid) != Guid.Empty));

			var listRepo = servLoc.GetInstance<ICmPossibilityListRepository>();

			foreach (int flid in customFieldIds)
			{
				Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
				string listCode = GetParentListCode(flid);
				lfCustomFieldLists[listCode] = listRepo.GetObject(parentListGuid);
			}

			return lfCustomFieldLists;
		}

		/// <summary>
		/// Write the custom field config into the provided dictionary
		/// </summary>
		/// <param name="lfCustomFieldConfig">Dictionary to receive LF custom field configuration settings (keys are field names
		/// as found in LF, e.g. customField_entry_MyCustomField)</param>
		public void WriteCustomFieldConfig(
			Dictionary<string, LfConfigFieldBase> lfCustomFieldConfig,
			Dictionary<string, string> lfCustomFieldTypes)
		{
			WriteCustomFieldConfigForOneFieldSourceType(LexEntryTags.kClassId, "entry",  lfCustomFieldConfig, lfCustomFieldTypes);
			WriteCustomFieldConfigForOneFieldSourceType(LexSenseTags.kClassId, "senses", lfCustomFieldConfig, lfCustomFieldTypes);
			WriteCustomFieldConfigForOneFieldSourceType(LexExampleSentenceTags.kClassId, "examples", lfCustomFieldConfig, lfCustomFieldTypes);
		}

		public void WriteCustomFieldConfigForOneFieldSourceType(
			int classId,
			string fieldSourceType,  // Can be either "entry", "senses", or "examples"
			Dictionary<string, LfConfigFieldBase> lfCustomFieldConfig,
			Dictionary<string, string> lfCustomFieldTypes
		)
		{
			IEnumerable<int> customFieldIds =
				fdoMetaData.GetFields(classId, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid));
			foreach (int flid in customFieldIds)
			{
				string label = fdoMetaData.GetFieldNameOrNull(flid);
				if (label == null)
					continue;
				string lfCustomFieldName = ConvertUtilities.NormalizedFieldName(label, fieldSourceType);
				CellarPropertyType fdoFieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
				lfCustomFieldTypes[lfCustomFieldName] = fdoFieldType.ToString();
				string lfCustomFieldType;
				if (CellarPropertyTypeToLfCustomFieldType.TryGetValue(fdoFieldType, out lfCustomFieldType))
				{
					// Get custom field configuration info
					LfConfigFieldBase fieldConfig = null;

					if (lfCustomFieldType.EndsWith("ListRef"))
					{
						// List references, whether single or multi, need a list code
						string listCode = GetParentListCode(flid);
						fieldConfig = GetLfCustomFieldOptionListConfig(label, lfCustomFieldType, listCode);
					}
					else if (lfCustomFieldType == CellarPropertyTypeToLfCustomFieldType[CellarPropertyType.OwningAtomic]) {
						// Multiparagraphs don't need writing systems
						fieldConfig = GetLfCustomFieldMultiParagraphConfig(label, lfCustomFieldType);
					}
					else
					{
						// Single line or MultiText fields need writing systems
						int fieldWs = fdoMetaData.GetFieldWs(flid);
						// That's a "magic" ws, which we need to expand into a (list of) real writing system(s).
#if FW8_COMPAT
						var wsesForThisField = new List<IWritingSystem>();
#else
						var wsesForThisField = new List<CoreWritingSystemDefinition>();
#endif
						// GetWritingSystemList() in FW 8.3 is buggy and doesn't properly handle the kwsAnal and kwsVern cases, so we handle them here instead.
						switch (fieldWs) {
						case WritingSystemServices.kwsAnal:
							wsesForThisField.Add(servLoc.LanguageProject.DefaultAnalysisWritingSystem);
							break;
						case WritingSystemServices.kwsVern:
							wsesForThisField.Add(servLoc.LanguageProject.DefaultVernacularWritingSystem);
							break;
						default:
							wsesForThisField = WritingSystemServices.GetWritingSystemList(cache, fieldWs, forceIncludeEnglish: false);
							break;
						}
#if FW8_COMPAT
						IEnumerable<string> inputSystems = wsesForThisField.Select(fdoWs => fdoWs.Id);
#else
						IEnumerable<string> inputSystems = wsesForThisField.Select(fdoWs => fdoWs.LanguageTag);
#endif
						// GetWritingSystemList returns all analysis WSes even when asked for just one, so if this
						// is a single-line custom field, trim the WSes down to just the first one
						if (lfCustomFieldType.StartsWith("Single"))
							inputSystems = inputSystems.Take(1);
						fieldConfig = GetLfCustomFieldMultiTextConfig(label, lfCustomFieldType, inputSystems.ToList());
					}

					if (fieldConfig != null)
						lfCustomFieldConfig[lfCustomFieldName] = fieldConfig;
				}
			}
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
		/// <param name="listConverters">Dictionary of ConvertFdoToMongoOptionList instances, keyed by list code</param>
		public BsonDocument GetCustomFieldsForThisCmObject(ICmObject cmObj, string objectType,
			IDictionary<string, ConvertFdoToMongoOptionList> listConverters)
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
				BsonDocument bsonForThisField = GetCustomFieldData(cmObj.Hvo, flid, objectType, listConverters);

				if (bsonForThisField != null)
				{
					customFieldData.Add(lfCustomFieldName, bsonForThisField["value"]);
					BsonValue guid;
					if (bsonForThisField.TryGetValue("guid", out guid))
					{
						if (guid is BsonArray)
							customFieldGuids.Add(lfCustomFieldName, guid, ((BsonArray)guid).Count > 0);
						else
							customFieldGuids.Add(lfCustomFieldName, guid);
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
		/// <returns>The list code as used in LF (e.g., "sense-type" or "grammatical-info").
		/// For custom lists from FDO, returns the user-given list name or abbreviation.</returns>
		/// <param name="flid">Flid.</param>
		private string GetParentListCode(int flid)
		{
			string result = string.Empty;
			Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
			if (parentListGuid != Guid.Empty)
			{
				if (GuidToListCode.TryGetValue(parentListGuid, out result))
					return result;
				// If it wasn't in GuidToListCode, it's a custom field we haven't actually seen yet
				ICmPossibilityList parentList;
				if (servLoc.GetInstance<ICmPossibilityListRepository>().TryGetObject(parentListGuid, out parentList))
				{
					if (parentList.Name != null)
						result = ConvertFdoToMongoTsStrings.TextFromTsString(parentList.Name.BestAnalysisVernacularAlternative, servLoc.WritingSystemFactory);
					if (!String.IsNullOrEmpty(result) && result != MagicStrings.UnknownString)
					{
						GuidToListCode[parentListGuid] = result;
						return result;
					}
					if (parentList.Abbreviation != null)
						result = ConvertFdoToMongoTsStrings.TextFromTsString(parentList.Abbreviation.BestAnalysisVernacularAlternative, servLoc.WritingSystemFactory);
					if (!String.IsNullOrEmpty(result) && result != MagicStrings.UnknownString)
					{
						GuidToListCode[parentListGuid] = result;

						return result;
					}
					result = String.Format("Custom List {0}", parentListGuid);
					GuidToListCode[parentListGuid] = result;
					return result;
				}
			}
			return result; // If we reach here, result is still an empty string
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
		/// <param name="listConverters">Dictionary of ConvertFdoToMongoOptionList instances, keyed by list code</param>
		private BsonDocument GetCustomFieldData(int hvo, int flid, string fieldSourceType,
			IDictionary<string, ConvertFdoToMongoOptionList> listConverters)
		{
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
					fieldValue = LfMultiText.FromSingleStringMapping(
						MagicStrings.LanguageCodeForIntFields, fieldValue.AsInt32.ToString()).AsBsonDocument();
				break;

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				fieldValue = GetCustomReferencedObject(ownedHvo, flid, listConverters, ref dataGuids);
				if (fieldValue != null && fdoFieldType == CellarPropertyType.ReferenceAtomic)
				{
					// Single CmPossiblity reference - LF expects format like { "value": "key of possibility" }
					fieldValue = new BsonDocument("value", fieldValue);
				}
				fieldGuid = new BsonString(dataGuids.FirstOrDefault().ToString());
				break;
			case CellarPropertyType.MultiUnicode:
				ITsMultiString tss = data.get_MultiStringProp(hvo, flid);
				if (tss != null && tss.StringCount > 0)
					fieldValue = LfMultiText.FromMultiITsString(tss, servLoc.WritingSystemManager).AsBsonDocument();
				break;
			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				var innerValues = new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid, listConverters, ref dataGuids)).Where(x => x != null));
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
					fieldValue = LfMultiText.FromSingleITsString(iTsValue, servLoc.WritingSystemManager).AsBsonDocument();
				break;
			default:
				fieldValue = null;
				if (logger != null)
					logger.Warning("FDO CellarPropertyType.{0} not recognized for LF custom field", fdoFieldType.ToString());
				break;
			}

			if (fieldValue == null)
				return null;
			else
			{
				var result = new BsonDocument();
				result.Add("value", fieldValue ?? BsonNull.Value); // BsonValues aren't allowed to have C# nulls; they have their own null representation
				if (fieldGuid is BsonArray)
					result.Add("guid", fieldGuid, ((BsonArray)fieldGuid).Count > 0);
				else
					result.Add("guid", fieldGuid, fieldGuid != null);
				return result;
			}
		}

		/// <summary>
		/// Gets the lf custom field option list config settings.
		/// </summary>
		/// <returns>The lf custom field settings as LfConfigMultiOptionList or LfConfigOptionList.</returns>
		/// <param name="label">Custom field label.</param>
		/// <param name="lfCustomFieldType">lf custom field type</param>
		/// <param name="listCode">Parent list code.</param>
		private LfConfigFieldBase GetLfCustomFieldOptionListConfig(string label, string lfCustomFieldType, string listCode)
		{
			if (lfCustomFieldType == CellarPropertyTypeToLfCustomFieldType[CellarPropertyType.ReferenceCollection])
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

		private LfConfigMultiText GetLfCustomFieldMultiTextConfig(string label, string lfCustomFieldType, List<string> inputSystems)
		{
			if (lfCustomFieldType == null)
				return null;
			return new LfConfigMultiText {
				Label = label,
				DisplayMultiline = !lfCustomFieldType.StartsWith("Single"),
				Width = 20,
				InputSystems = inputSystems,
			};
		}

		private LfConfigMultiParagraph GetLfCustomFieldMultiParagraphConfig(string label, string lfCustomFieldType)
		{
			if (lfCustomFieldType == null)
				return null;
			return new LfConfigMultiParagraph {
				Label = label,
			};
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
		/// <param name="listConverters">Dictionary of ConvertFdoToMongoOptionList instances, keyed by list code</param>
		/// <param name="referencedObjectGuids">List to which referenced object's GUID will be added.</param>
		private BsonValue GetCustomReferencedObject(int hvo, int flid,
			IDictionary<string, ConvertFdoToMongoOptionList> listConverters,
			ref List<Guid> referencedObjectGuids)
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
					servLoc.WritingSystemManager, fdoMetaData, cache.DefaultUserWs);
			else if (referencedObject is ICmPossibility)
			{
				//return GetCustomListValues((ICmPossibility)referencedObject, flid);
				string listCode = GetParentListCode(flid);
				return new BsonString(listConverters[listCode].LfItemKeyString((ICmPossibility)referencedObject, _wsEn));
			}
			else
				return null;
		}
	}
}

