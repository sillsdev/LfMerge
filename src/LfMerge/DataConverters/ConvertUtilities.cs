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

		/// <summary>
		/// Turn a custom StText field into a BsonDocument suitable for storing in Mongo. Returns
		/// </summary>
		/// <returns>A BsonDocument with the following structure:
		/// { "en": { "value": "<p>Para 1</p><p>Para 2</p>", "guids": ["Guid for para 1", "Guid for para 2"] } }
		/// </returns>
		/// <param name="obj">StText whose contents we want.</param>
		/// <param name="flid">Field ID for this custom StText field (used to get correct writing system for this StText).</param>
		/// <param name="wsManager">IWritingSystemManager object from FDO, used to get writing system names from integer IDs.</param>
		/// <param name="metaDataCacheAccessor">Meta data cache accessor from FDO, used to get the integer ID of this field's writing system.</param>
		/// <param name="fallbackWs">Writing system to fall back to if we can't figure it out any other way (usually the default user ws).</param>
		public static BsonValue GetCustomStTextValues(IStText obj, int flid,
			IWritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int fallbackWs)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			// Get paragraph contents and GUIDs
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Where(para => para.Contents != null).Select(para => para.Contents).ToList();
			List<Guid> guids = obj.ParagraphsOS.OfType<IStTxtPara>().Where(para => para.Contents != null).Select(para => para.Guid).ToList();
			List<string> htmlParas = paras.Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			// Prepare the inner BsonDocument
			var bsonParas = new BsonString(String.Join("", htmlParas));
			var bsonGuids = new BsonArray(guids);
			var bsonResult = new BsonDocument(new Dictionary<string, object> { { "value", bsonParas }, { "guids", bsonGuids } });
			// And wrap the whole thing in a BsonDocument keyed by the field's primary writing system
			int fieldWs = metaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(fallbackWs);
			return new BsonDocument(wsStr, bsonResult);
		}

		private struct LfTxtPara { // For SetCustomStTextValues -- nicer than using a tuple
			public Guid Guid;
			public string Contents;
		}
		// TODO: If LanguageForge allows editing paragraph styles in multi-paragraph fields, the styleForNewParas parameter
		// will need to become a list of styles, one per paragraph. At that point we might as well add the style to the LfTxtPara
		// struct, and pass a list of such structs into SetCustomStTextValues.
		public static void SetCustomStTextValues(IStText fdoStText, IEnumerable<Guid> lfGuids, IEnumerable<string> lfParaContents, int wsId, string styleForNewParas)
		{
			LfTxtPara[] lfParas = lfGuids.Zip(lfParaContents, (g, s) => new LfTxtPara { Guid=g, Contents=s }).ToArray();
			// Step 1: Delete all FDO paragraphs that are no longer found in LF
			var guidsInLf = new HashSet<Guid>(lfGuids);
			var parasToDelete = new HashSet<IStTxtPara>();
			for (int i = 0, count = fdoStText.ParagraphsOS.Count; i < count; i++)
			{
				IStTxtPara para = fdoStText[i];
				if (!guidsInLf.Contains(para.Guid))
					parasToDelete.Add(para);
			}
			// Step 2: Step through LF and FDO paragraphs *by integer index* and copy texts over, adding new paras as needed
			for (int i = 0, count = lfParas.Length; i < count; i++)
			{
				LfTxtPara lfPara = lfParas[i];
				IStTxtPara fdoPara;
				if (i >= fdoStText.ParagraphsOS.Count)
				{
					// Past the end of existing FDO paras: create new para at end
					fdoPara = fdoStText.AddNewTextPara(styleForNewParas);
				}
				else
				{
					fdoPara = fdoStText[i];
					if (fdoPara.Guid != lfPara.Guid)
					{
						// A new para was inserted into LF at this point; duplicate that in FDO
						fdoPara = fdoStText.InsertNewTextPara(i, styleForNewParas);
					}
				}
				fdoPara.Contents = TsStringUtils.MakeTss(lfPara.Contents, wsId);
			}
		}
	}
}
