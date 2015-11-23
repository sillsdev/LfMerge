// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LfMerge.LanguageForge.Model;
using MongoDB.Bson;
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

		// Returns a tuple of (customFields value, customFieldGuids value)
		public Tuple<BsonDocument, Dictionary<string, List<Guid>>> CustomFieldsForThisCmObject(ICmObject cmObj)
		{
			if ((cmObj) == null) return null;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var customFieldData = new BsonDocument();
			var customFieldGuids = new Dictionary<string, List<Guid>>();

			foreach (int flid in customFieldIds)
			{
				string fieldName;
				List<Guid> fieldDataGuids;
				BsonValue bson = GetCustomFieldData(cmObj.Hvo, flid, out fieldName, out fieldDataGuids);
				// TODO: Need to convert field name to, say, underscores instead of spaces and so on.
				if (bson != null)
				{
					customFieldData.Add(fieldName, bson);
					customFieldGuids.Add(fieldName, fieldDataGuids);
				}
			}

			return Tuple.Create(customFieldData, customFieldGuids);
		}

		/// <summary>
		/// Gets custom field data in a format suitable for .
		/// </summary>
		/// <returns>The custom field data.</returns>
		/// <param name="hvo">HVO of object whose data should be retrieved.</param>
		/// <param name="flid">Field ID of the custom field.</param>
		/// <param name="dataGuid">If custom field data is an object with a GUID, store it here.</param>
		private BsonValue GetCustomFieldData(int hvo, int flid, out string fieldName, out List<Guid> dataGuids)
		{
			// TODO: Actually, a single "out Guid" parameter probably won't work... how does the caller learn the field name?
			// Either we store name and guid in a dict (passed in by reference?) or we use two out parameters, one Guid and one string (name).
			//
			// TODO: Or maybe we should return a struct from this function, with three fields:
			// result.bsonValue (a BsonValue)
			// result.fieldName (a string)
			// result.dataGuids (a list of Guids)
			// If result.dataGuids has length 1, 
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			dataGuids = new List<Guid>();
			fieldName = fdoMetaData.GetFieldNameOrNull(flid);
			if (fieldName == null)
				return null;

			switch (fieldType)
			{
			case CellarPropertyType.Binary:
			case CellarPropertyType.Image: // Treat image fields as binary blobs
				byte[] binaryData;
				data.get_Binary(hvo, flid, out binaryData);
				return (binaryData == null) ? null : new BsonBinaryData(binaryData);

			case CellarPropertyType.Boolean:
				return new BsonBoolean(data.get_BooleanProp(hvo, flid));

			case CellarPropertyType.Float:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				return null;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.GenDate:
				GenDate genDate = data.get_GenDateProp(hvo, flid);
				string genDateStr = genDate.ToLongString();
				return (String.IsNullOrEmpty(genDateStr)) ? null : new BsonString(genDateStr);
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Guid:
				// Note that we do NOT add anything to dataGuids here. That's only for objects (e.g., OwningAtomic or ReferenceAtomic data)
				return new BsonString(data.get_GuidProp(hvo, flid).ToString());

			case CellarPropertyType.Integer:
				return new BsonInt32(data.get_IntProp(hvo, flid));

			case CellarPropertyType.MultiString:
			case CellarPropertyType.MultiUnicode:
				var fdoMultiString = (IMultiAccessorBase)data.get_MultiStringProp(hvo, flid);
				LfMultiText multiTextValue = LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
				return (multiTextValue == null || multiTextValue.Count == 0) ? null : new BsonDocument(multiTextValue.AsStringDictionary());

			case CellarPropertyType.Nil:
				return null;

			case CellarPropertyType.Numeric:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				return null;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				Guid dataGuid;
				BsonValue atomBsonValue = GetCustomReferencedObject(ownedHvo, flid, out dataGuid);
				dataGuids.Add(dataGuid);
				return atomBsonValue;

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] referencedObjectHvos = data.VecProp(hvo, flid);
				List<BsonValue> values = new List<BsonValue>();
				foreach (int thisHvo in referencedObjectHvos)
				{
					Guid thisGuid;
					values.Add(GetCustomReferencedObject(thisHvo, flid, out thisGuid));
					dataGuids.Add(thisGuid);
				}
				return new BsonArray(values);

			case CellarPropertyType.String:
				ITsString iTsValue = data.get_StringProp(hvo, flid);
				if (iTsValue == null || String.IsNullOrEmpty(iTsValue.Text))
					return null;
				else
					return new BsonString(iTsValue.Text);

			case CellarPropertyType.Unicode:
				string UnicodeValue = data.get_UnicodeProp(hvo, flid);
				return (String.IsNullOrEmpty(UnicodeValue)) ? null : new BsonString(UnicodeValue);

			case CellarPropertyType.Time:
				return new BsonDateTime(data.get_DateTime(hvo, flid));

			default:
				return null;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
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

		private BsonValue GetCustomReferencedObject(int hvo, int flid, out Guid referencedObjectGuid)
		{
			referencedObjectGuid = Guid.Empty;
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			if (hvo == 0 || !data.get_IsValidObject(hvo)) return null;
			ICmObject referencedObject = cache.GetAtomicPropObject(hvo);
			referencedObjectGuid = referencedObject.Guid;
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

		public bool SetCustomFieldData(int hvo, int flid, BsonValue value, List<Guid> dataGuids)
		{
			if (value == null)
				return false;
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
				Guid guid;
				if (Guid.TryParse(value.AsString, out guid))
				{
					data.SetGuid(hvo, flid, guid);
					return true;
				}
				return false;

			case CellarPropertyType.Integer:
				data.SetInt(hvo, flid, value.AsInt32);
				return true;

			case CellarPropertyType.MultiString: // TODO: Write this one
			case CellarPropertyType.MultiUnicode:
				// Step 1: deserialize BsonDocument value as an LfMultiText.
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
				// TODO: Use data.get_ObjFromGuid(guid) for this somehow;
				return false;
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
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				// return new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid)));
				return false;

			case CellarPropertyType.String:
				// data.SetString(...)
				return false; // TODO: Implement this. Make an ITsString somehow.
//				ITsString iTsValue = data.get_StringProp(hvo, flid);
//				if (iTsValue == null || String.IsNullOrEmpty(iTsValue.Text))
//					return null;
//				else
//					return new BsonString(iTsValue.Text);

			case CellarPropertyType.Unicode:
				string valueStr = value.AsString;
				data.SetUnicode(hvo, flid, valueStr, valueStr.Length);
				return true;

			case CellarPropertyType.Time:
				data.SetDateTime(hvo, flid, value.ToUniversalTime());
				return true;

			default:
				return false;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			return false;
		}
	}
}

