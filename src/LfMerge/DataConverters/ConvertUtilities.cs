// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.DataConverters
{
	public class ConvertUtilities
	{
		/// <summary>
		/// Normalizes a fieldName string so it can be stored in Mongo
		/// </summary>
		/// <returns>The normalized field name string.</returns>
		/// <param name="fieldName">Field name.</param>
		/// <param name="fieldSourceType">Either "entry", "senses" or "examples". Could also be "allomorphs", eventually.</param>
		public static string NormalizedFieldName(string fieldName, string fieldSourceType)
		{
			fieldName = fieldName.Replace(' ', '_');
			return String.Format("customField_{0}_{1}", fieldSourceType, fieldName);
		}

		public static BsonValue GetCustomStTextValues(IStText obj, int flid,
			IWritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int defaultUserWs)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Select(para => para.Contents).ToList();
			List<string> htmlParas = paras.Where(para => para != null).Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			int fieldWs = metaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(defaultUserWs); // TODO: Should that be DefaultAnalWs instead?
			return new BsonDocument(wsStr, new BsonDocument("value", new BsonString(String.Join("", htmlParas))));
		}
	}
}
