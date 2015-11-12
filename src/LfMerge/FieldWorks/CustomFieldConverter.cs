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

		private LfMultiText ToMultiText(IMultiAccessorBase fdoMultiString)
		{
			if (fdoMultiString == null) return null;
			return LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
		}

		private BsonDocument CustomFieldsForLexEntry(ILexEntry fdoEntry)
		{
			if ((fdoEntry) == null) return null;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(fdoEntry.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var result = new BsonDocument();

			foreach (int flid in customFieldIds)
			{
				BsonValue bson = GetCustomFieldData(fdoEntry.Hvo, flid);
				// TODO: Need to convert field name to, say, underscores instead of spaces and so on.
				result.Add(fdoMetaData.GetFieldName(flid), bson);
			}

			return result;
		}

		private BsonValue GetCustomFieldData(int hvo, int flid)
		{
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
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
				return new BsonString(genDate.ToLongString());
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Guid:
				return new BsonString(data.get_GuidProp(hvo, flid).ToString());

			case CellarPropertyType.Integer:
				return new BsonInt32(data.get_IntProp(hvo, flid));

			case CellarPropertyType.MultiString:
			case CellarPropertyType.MultiUnicode:
				LfMultiText multiTextValue = ToMultiText((IMultiAccessorBase)data.get_MultiStringProp(hvo, flid));
				return (multiTextValue == null) ? null : new BsonDocument(multiTextValue.AsStringDictionary());

			case CellarPropertyType.Nil:
				return null;

			case CellarPropertyType.Numeric:
				// Floating-point fields are currently not allowed in FDO (as of 2015-11-12)
				return null;
				// TODO: Maybe issue a proper warning (or error) log message?

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				return GetCustomReferencedObject(ownedHvo, flid);

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				return new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid)));

			case CellarPropertyType.String:
				ITsString iTsValue = data.get_StringProp(hvo, flid);
				if (iTsValue == null || iTsValue.Text == null)
					return null;
				else
					return new BsonString(iTsValue.Text);

			case CellarPropertyType.Unicode:
				string UnicodeValue = data.get_UnicodeProp(hvo, flid);
				return (UnicodeValue == null) ? null : new BsonString(UnicodeValue);

			case CellarPropertyType.Time:
				return new BsonDateTime(data.get_DateTime(hvo, flid));

			default:
				return null;
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
			return new BsonDocument(wsStr, new BsonString(String.Join("", htmlParas)));
		}

		private BsonValue GetCustomListValues(ICmPossibility obj, int flid)
		{
			if (obj == null) return null;
			// TODO: If obj.Name.BestAnalysisVernacularAlternative.Text fails, break it down in small parts will null checks.
			return new BsonString(obj.Name.BestAnalysisVernacularAlternative.Text);
		}

		private BsonValue GetCustomReferencedObject(int hvo, int flid)
		{
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			if (!data.get_IsValidObject(hvo)) return null;
			ICmObject referencedObject = cache.GetAtomicPropObject(hvo);
			if (referencedObject is IStText)
				return GetCustomStTextValues((IStText)referencedObject, flid);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
		}
	}
}

