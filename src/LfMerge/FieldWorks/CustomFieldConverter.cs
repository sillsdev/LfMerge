// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LfMerge.LanguageForge.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.FieldWorks
{
	public class CustomFieldConverter
	{
		private FdoCache cache;
		private IFdoServiceLocator servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;

		public CustomFieldConverter(FdoCache cache)
		{
			this.cache = cache;
			servLoc = cache.ServiceLocator;
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
		}

		/// <summary>
		/// Customs the fields for this cm object.
		/// </summary>
		/// <returns>A BsonDocument with the following structure: <br />
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
		/// </returns>
		/// <param name="cmObj">Cm object.</param>
		/// <param name="objectType">Either "entry", "senses", or "examples"</param>
		public BsonDocument CustomFieldsForThisCmObject(ICmObject cmObj, string objectType = "entry")
		{
			if ((cmObj) == null) return null;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var customFieldData = new BsonDocument();
			var customFieldGuids = new BsonDocument();

			foreach (int flid in customFieldIds)
			{
				List<Guid> fieldDataGuids;
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return null;
				fieldName = NormalizedFieldName(fieldName, objectType);
				BsonDocument bsonForThisField = GetCustomFieldData(cmObj.Hvo, flid);
				// TODO: Need to convert field name to, say, underscores instead of spaces and so on.
				if (bsonForThisField != null)
				{
					customFieldData.Add(fieldName, bsonForThisField["value"]);
					BsonValue guid;
					if (bsonForThisField.TryGetValue("guid", out guid))
						customFieldGuids.Add(fieldName, guid);
				}
			}

			BsonDocument result = new BsonDocument();
			result.Add("customFields", customFieldData);
			result.Add("customFieldGuids", customFieldGuids);
			return result;
		}

		private string NormalizedFieldName(string fieldName, string fieldSourceType)
		{
			fieldName = fieldName.Replace(' ', '_');
			return String.Format("customField_{0}_{1}", fieldSourceType, fieldName);
		}

		/// <summary>
		/// Gets the data for one custom field, and any relevant GUIDs.
		/// </summary>
		/// <returns>A BsonDocument with the following structure: <br />
		/// { fieldName: { "value": BsonValue, "guid": "some-guid-as-a-string" } } <br />
		/// -OR- <br />
		/// { fieldName: { "value": BsonValue, "guids": ["guid1", "guid2", "guid3"] } } <br />
		/// The format of the fieldName key will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// The presense of either the "guid" or "guids" key will determine whether there is a string or array following it.
		/// (TODO: Fix this documentation since now we always have the key "guid")
		/// If there is neither "guid" nor "guids", that field has no need for a GUID. (E.g., a number).
		/// </returns>
		/// <param name="hvo">Hvo of object we're getting the field for.</param>
		/// <param name="flid">Flid for this field.</param>
		/// <param name="fieldType">Either "entry", "senses" or "examples". Could also be "allomorphs", eventually.</param>
		private BsonDocument GetCustomFieldData(int hvo, int flid, string fieldSourceType = "entry")
		{
			// TODO: Rewrite this to return a BsonDocument instead of a BsonValue.
			// Returned BsonDocument will contain two keys:
			// "customFields": value is a BsonDocument with one key per field name, value depending on field type
			// "customFieldGuids": same thing, but value is either one GUID, or a list of GUIDs. (Or a key can be omitted).
			BsonValue fieldValue = null;
			BsonValue fieldGuid = null; // Might be a single value, might be a list (as a BsonArray)
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			var dataGuids = new List<Guid>();

			switch (fieldType)
			{
			case CellarPropertyType.Binary:
			case CellarPropertyType.Image: // Treat image fields as binary blobs
				byte[] binaryData;
				data.get_Binary(hvo, flid, out binaryData);
				fieldValue = (binaryData == null) ? null : new BsonBinaryData(binaryData);
				break;

			case CellarPropertyType.Boolean:
				fieldValue = new BsonBoolean(data.get_BooleanProp(hvo, flid));
				break;

			case CellarPropertyType.Float:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				fieldValue = null;
				break;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.GenDate:
				GenDate genDate = data.get_GenDateProp(hvo, flid);
				string genDateStr = genDate.ToLongString();
				fieldValue = (String.IsNullOrEmpty(genDateStr)) ? null : new BsonString(genDateStr);
				break;
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Guid:
				// Note that we do NOT set fieldGuid here. That's only for objects (e.g., OwningAtomic or ReferenceAtomic data)
				fieldValue = new BsonString(data.get_GuidProp(hvo, flid).ToString());
				break;

			case CellarPropertyType.Integer:
				fieldValue = new BsonInt32(data.get_IntProp(hvo, flid));
				break;

			case CellarPropertyType.MultiString:
			case CellarPropertyType.MultiUnicode:
				var fdoMultiString = (IMultiAccessorBase)data.get_MultiStringProp(hvo, flid);
				LfMultiText multiTextValue = LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
				fieldValue = (multiTextValue == null || multiTextValue.Count == 0) ? null : new BsonDocument(multiTextValue.AsStringDictionary());
				// TODO: Do we need to set fieldGuid here? Probably not, but research it.
				break;

			case CellarPropertyType.Nil:
				fieldValue = null;
				break;

			case CellarPropertyType.Numeric:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				fieldValue = null;
				// TODO: Maybe issue a proper warning (or error) log message?
				break;

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				fieldValue = GetCustomReferencedObject(ownedHvo, flid, ref dataGuids);
				fieldGuid = new BsonString(dataGuids.FirstOrDefault().ToString());
				break;

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				fieldValue = new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid, ref dataGuids)).Where(x => x != null));
				fieldGuid = new BsonArray(dataGuids.Select(guid => guid.ToString()));
				break;

			case CellarPropertyType.String:
				ITsString iTsValue = data.get_StringProp(hvo, flid);
				if (iTsValue == null || String.IsNullOrEmpty(iTsValue.Text))
					fieldValue = null;
				else
					fieldValue = new BsonString(iTsValue.Text);
				break;

			case CellarPropertyType.Unicode:
				string UnicodeValue = data.get_UnicodeProp(hvo, flid);
				fieldValue = (String.IsNullOrEmpty(UnicodeValue)) ? null : new BsonString(UnicodeValue);
				break;

			case CellarPropertyType.Time:
				fieldValue = new BsonDateTime(data.get_DateTime(hvo, flid));
				break;

			default:
				fieldValue = null;
				break;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			var result = new BsonDocument();
			result.Add("value", fieldValue ?? BsonNull.Value); // BsonValues aren't allowed to have C# nulls; they have their own null representation
			if (fieldGuid is BsonArray)
				result.Add("guid", fieldGuid, ((BsonArray)fieldGuid).Count > 0);
			else
				result.Add("guid", fieldGuid, fieldGuid != null);
			return result;
		}

		private BsonValue GetCustomStTextValues(IStText obj, int flid)
		{
			if (obj == null) return null;
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Select(para => para.Contents).ToList();
			List<string> htmlParas = paras.Where(para => para != null).Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			IWritingSystemManager wsManager = cache.ServiceLocator.WritingSystemManager;
			int fieldWs = cache.MetaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs); // TODO: Should this be cache.DefaultUserWs instead?
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(cache.DefaultUserWs);
			return new BsonDocument(wsStr, new BsonString(String.Join("", htmlParas)));
		}

		private BsonValue GetCustomListValues(ICmPossibility obj, int flid)
		{
			if (obj == null) return null;
			// TODO: If obj.Name.BestAnalysisVernacularAlternative.Text fails, break it down in small parts will null checks.
			return new BsonString(obj.Name.BestAnalysisVernacularAlternative.Text);
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
				return GetCustomStTextValues((IStText)referencedObject, flid);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
		}

		// TODO: Determine what return type we want. Maybe a Dictionary<string, ICmObject> would work.
		public object ParseCustomFields(BsonDocument customFields)
		{
			throw new NotImplementedException(); // TODO: Implement this
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result = default(Guid);
			Guid.TryParse(input, out result);
			return result;
		}

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
			if (fieldName == null)
				return false;

			switch (fieldType)
			{
			case CellarPropertyType.Binary:
			case CellarPropertyType.Image: // Treat image fields as binary blobs
				byte[] bytes = value.AsBsonBinaryData.Bytes;
				data.SetBinary(hvo, flid, bytes, bytes.Length);
				return true;

			case CellarPropertyType.Boolean:
				data.SetBoolean(hvo, flid, value.AsBoolean);
				return true;

			case CellarPropertyType.Float:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				return false;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.GenDate:
				GenDate genDate; // = data.get_GenDateProp(hvo, flid);
				if (GenDate.TryParse(value.AsString, out genDate))
				{
					data.SetGenDate(hvo, flid, genDate);
					return true;
				}
				return false;
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Guid:
				Guid valueAsGuid;
				if (Guid.TryParse(value.AsString, out valueAsGuid))
				{
					data.SetGuid(hvo, flid, valueAsGuid);
					return true;
				}
				return false;

			case CellarPropertyType.Integer:
				data.SetInt(hvo, flid, value.AsInt32);
				return true;

			case CellarPropertyType.MultiString: // TODO: Write this one
			case CellarPropertyType.MultiUnicode:
				// Step 1: deserialize BsonDocument value as an LfMultiText.
				LfMultiText bar = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
				Console.WriteLine("Custom field {0} contained MultiText that looks like:", fieldName);
				foreach (var kv in bar.AsStringDictionary())
					Console.WriteLine("  {0}: {1}", kv.Key, kv.Value);
				// Step 2: Use a bunch of data.SetMultiStringAlt calls?? TODO: Not sure that's right.
				return false;
				/*
				var fdoMultiString = (IMultiAccessorBase)data.get_MultiStringProp(hvo, flid);
				LfMultiText multiTextValue = LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
				return (multiTextValue == null || multiTextValue.Count == 0) ? null : new BsonDocument(multiTextValue.AsStringDictionary());
*/
			case CellarPropertyType.Nil:
				data.SetUnknown(hvo, flid, null);
				return true;

			case CellarPropertyType.Numeric:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				return false;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.OwningAtomic:
				return false; // TODO: Need to implement this one

			case CellarPropertyType.ReferenceAtomic:
				if (fieldGuids.First() != Guid.Empty)
				{
					int referencedHvo = data.get_ObjFromGuid(fieldGuids.First());
					data.SetObjProp(hvo, flid, referencedHvo);
					return true;
				}
				else
				{
					// What do we do if the object isn't yet in FDO? TODO: Consider this case.
					return false;
				}
				// int ownedHvo = data.get_ObjectProp(hvo, flid);
				// return GetCustomReferencedObject(ownedHvo, flid, out dataGuid);
				/*
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			if (hvo == 0 || !data.get_IsValidObject(hvo)) return null;
			ICmObject referencedObject = cache.GetAtomicPropObject(hvo);
			if (referencedObject is IStText)
				return GetCustomStTextValues((IStText)referencedObject, flid);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
				*/

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
				// NOT implemented since currently, FDO custom fields CANNOT be OwningCollection or OwningSequence.
				// This is true as of 2015-11-23. If it ever changes, this will need to be implemented.
				return false;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				{
					if (value == null || value == BsonNull.Value) return false;
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
					if (parentListGuid == Guid.Empty)
					{
						Console.WriteLine("No possibility list found for custom field {0}; giving up", fieldName);
						return false; // TODO: Is this really possible?? If not, remove this check
					}
					ICmPossibilityList parentList = (ICmPossibilityList)servLoc.GetObject(parentListGuid);

					LfStringArrayField valueAsStringArray = BsonSerializer.Deserialize<LfStringArrayField>(value.AsBsonDocument);

					// Step 1: Check if any of the fieldGuids is Guid.Empty, which would indicate a brand-new object that wasn't in FDO
					List<string> fieldData = valueAsStringArray.Values;
					Console.WriteLine("Reference collection had values {0}", String.Join(", ", fieldData));
					Console.WriteLine("Reference collection had GUIDs {0}", guidOrGuids.ToJson());
					List<ICmPossibility> fieldObjs = new List<ICmPossibility>();
					fieldGuids.Zip<Guid, string, bool>(fieldData, (thisGuid, thisData) =>
					{
						ICmPossibility newPoss;
						if (thisGuid == default(Guid)) {
							newPoss = ((ICmPossibilityList)parentList).FindOrCreatePossibility(thisData, fieldWs);
							fieldObjs.Add(newPoss);
						}
						else {
							newPoss = servLoc.GetObject(thisGuid) as ICmPossibility;
							fieldObjs.Add(newPoss);
						}
						Console.WriteLine("Got possibility ({0}) with GUID {1} when looking up GUID {2}", newPoss.AbbrAndName, newPoss.Guid, thisGuid);
						return true;
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
					foreach (var newHvo in newHvosArray)
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
				Console.WriteLine("Got value {0} of type {1}", value, value.GetType());
				Console.WriteLine("Writing system #{0} is \"{1}\" for this field", fdoMetaData.GetFieldWs(flid), servLoc.WritingSystemManager.GetStrFromWs(fdoMetaData.GetFieldWs(flid)));
				var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
				data.SetString(hvo, flid, TsStringUtils.MakeTss(valueAsMultiText.FirstNonEmptyString(), cache.DefaultAnalWs));
				// TODO: Somehow use WritingSystemServices.ActualWs to get the right writing system here, instead of just assuming analysis
				return true;

			case CellarPropertyType.Unicode:
				{
					string valueStr = value.AsString;
					data.SetUnicode(hvo, flid, valueStr, valueStr.Length);
					return true;
				}

			case CellarPropertyType.Time:
				data.SetDateTime(hvo, flid, value.ToUniversalTime());
				return true;

			default:
				return false;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			return false; // If compiler complains about unreachable code, GOOD! We got the switch statement right. Otherwise this is our catch-all.
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
				// TODO:
				// To deal with that case, we'd need to store a list of field names we've gotten from Mongo, then compare to the
				// field names we've gotten
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return;
				fieldName = NormalizedFieldName(fieldName, objectType);
				BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				BsonValue fieldGuidOrGuids = (customFieldGuids == null) ? BsonNull.Value : customFieldGuids.GetValue(fieldName, BsonNull.Value);
				remainingFieldNames.Remove(fieldName);
				Console.WriteLine("Setting custom field {0} with data {1} and GUID(s) {2}", fieldName, fieldValue.ToJson(), fieldGuidOrGuids.ToJson());
				// TODO: Detect when fieldValue is null and don't bother calling SetCustomFieldData
				SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				customFieldValues.Remove(fieldName);
				if (customFieldGuids != null && customFieldGuids != BsonNull.Value)
					customFieldGuids.Remove(fieldName);
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

