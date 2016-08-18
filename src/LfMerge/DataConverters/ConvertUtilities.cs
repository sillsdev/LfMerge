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
using SIL.FieldWorks.FDO.DomainServices;
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
		/// Make an LfParagraph object from an FDO StTxtPara.
		/// </summary>
		/// <returns>The LFParagraph.</returns>
		/// <param name="fdoPara">FDO StTxtPara object to convert.</param>
		public static LfParagraph FdoParaToLfPara(IStTxtPara fdoPara, ILgWritingSystemFactory wsf)
		{
			var lfPara = new LfParagraph();
			lfPara.Guid = fdoPara.Guid;
			lfPara.StyleName = fdoPara.StyleName;
			lfPara.Content = ConvertFdoToMongoTsStrings.TextFromTsString(fdoPara.Contents, wsf);
			return lfPara;
		}

		/// <summary>
		/// Turn a custom StText field into a BsonDocument suitable for storing in Mongo. Returns
		/// </summary>
		/// <returns>A BsonDocument with the following structure:
		/// { "ws": "en",
		///   "paras": [ { "guid": "123", "styleName": "normal", "contents": "First paragraph" },
		///              { "guid": "456", "styleName": "italic", "contents": "Second paragraph" } ] }
		/// </returns>
		/// <param name="obj">StText whose contents we want.</param>
		/// <param name="flid">Field ID for this custom StText field (used to get correct writing system for this StText).</param>
		/// <param name="wsManager">IWritingSystemManager object from FDO, used to get writing system names from integer IDs.</param>
		/// <param name="metaDataCacheAccessor">Meta data cache accessor from FDO, used to get the integer ID of this field's writing system.</param>
		/// <param name="fallbackWs">Writing system to fall back to if we can't figure it out any other way (usually the default user ws).</param>
		public static BsonValue GetCustomStTextValues(IStText obj, int flid,
			IWritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int fallbackWs)
		{
			LfMultiParagraph result = GetCustomStTextValuesAsLfMultiPara(obj, flid, wsManager, metaDataCacheAccessor, fallbackWs);
			return result.ToBsonDocument();
		}

		public static LfMultiParagraph GetCustomStTextValuesAsLfMultiPara(IStText obj, int flid,
			IWritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int fallbackWs)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			var result = new LfMultiParagraph();
			result.Paragraphs = obj.ParagraphsOS.OfType<IStTxtPara>().Where(para => para.Contents != null).Select(para => FdoParaToLfPara(para, wsManager)).ToList();
			// StText objects in FDO have a single primary writing system, unlike MultiString or MultiUnicode objects
			int fieldWs = metaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(fallbackWs);
			result.InputSystem = wsStr;
			return result;
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
				fdoPara.Contents = ConvertMongoToFdoTsStrings.SpanStrToTsString(lfPara.Contents, wsId, fdoStText.Cache.WritingSystemFactory);
			}
		}

		public static void SetCustomStTextValues(IStText fdoStText, IEnumerable<LfParagraph> lfParas, int wsId)
		{
			// Output format:
			// { "ws": "en",
			//   "paras": [ { "guid": "123", "styleName": "normal", "contents": "First paragraph" },
			//              { "guid": "456", "styleName": "italic", "contents": "Second paragraph" } ] }

			// Step 1: Delete all FDO paragraphs that are no longer found in LF
			var guidsInLf = new HashSet<Guid>(lfParas.Where(p => p.Guid != null).Select(p => p.Guid.Value));
			var parasToDelete = new HashSet<IStTxtPara>();
			for (int i = 0, count = fdoStText.ParagraphsOS.Count; i < count; i++)
			{
				IStTxtPara para = fdoStText[i];
				if (!guidsInLf.Contains(para.Guid))
					parasToDelete.Add(para);
			}
			// Step 2: Step through LF and FDO paragraphs, adding new paragraphs as needed.
			// (We step through FDO paras *by integer index* so that inserting new paragraphs into FDO will place them in the right location)
			int fdoIdx = 0;
			foreach (LfParagraph lfPara in lfParas)
			{
				IStTxtPara fdoPara;
				if (fdoIdx >= fdoStText.ParagraphsOS.Count)
				{
					// Past the end of existing FDO paras: create new para at end
					Console.WriteLine("Appending new para with style name {0} and contents {1}", lfPara.StyleName, lfPara.Content);
					fdoPara = fdoStText.AddNewTextPara(lfPara.StyleName);
				}
				else
				{
					fdoPara = fdoStText[fdoIdx];
					if (fdoPara.Guid != lfPara.Guid)
					{
						// A new para was inserted into LF at this point; duplicate that in FDO
						fdoPara = fdoStText.InsertNewTextPara(fdoIdx, lfPara.StyleName);
					}
				}
				fdoPara.Contents = ConvertMongoToFdoTsStrings.SpanStrToTsString(lfPara.Content, wsId, fdoStText.Cache.WritingSystemFactory);
				// It turns out that FDO often has an empty StyleName for the normal, default paragraph style. So in those
				// cases, where we've gotten an empty StyleName in the LfParagraph object, we should NOT change it to be
				// the default paragraph style, as that can cause round-tripping problems.
				#if false
				if (String.IsNullOrEmpty(lfPara.StyleName))
				{
					lfPara.StyleName = defaultParagraphStyle; // NOTE: The "defaultParagraphStyle" parameter has been removed
				}
				#endif
				if (!String.IsNullOrEmpty(lfPara.StyleName)) // Not allowed to set an empty style name on an FDO paragraph
				{
					fdoPara.StyleName = lfPara.StyleName;
				}

				fdoIdx++;
			}
		}

		// Convenience overload
		public static void SetCustomStTextValues(IStText fdoStText, IEnumerable<LfParagraph> lfParas)
		{
			SetCustomStTextValues(fdoStText, lfParas, fdoStText.MainWritingSystem);
		}
	}
}
