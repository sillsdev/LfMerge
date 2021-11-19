// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.LanguageForge.Model;
using MongoDB.Bson;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;

namespace LfMerge.Core.DataConverters
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
		/// Make an LfParagraph object from an LCM StTxtPara.
		/// </summary>
		/// <returns>The LFParagraph.</returns>
		/// <param name="lcmPara">LCM StTxtPara object to convert.</param>
		public static LfParagraph LcmParaToLfPara(IStTxtPara lcmPara, ILgWritingSystemFactory wsf)
		{
			var lfPara = new LfParagraph();
			lfPara.Guid = lcmPara.Guid;
			lfPara.StyleName = lcmPara.StyleName;
			lfPara.Content = ConvertLcmToMongoTsStrings.TextFromTsString(lcmPara.Contents, wsf);
			return lfPara;
		}

		public static bool ReplaceHvosInCustomField(int hvo, int flid, ISilDataAccessManaged data, int[] oldArray, int[] newArray)
		{
			// Shortcut check
			if (oldArray.SequenceEqual(newArray))
			{
				// Nothing to do, so return now so that we don't cause unnecessary changes and commits in Mercurial
				return false;
			}
			// HashSets for O(1) lookup. Might be overkill, but better safe than sorry
			var newHvos = new HashSet<int>(newArray);
			var combinedHvos = new HashSet<int>();

			// Step 1: Remove any objects from the "old" list that weren't in the "new" list
			// Loop backwards so deleting items won't mess up indices of subsequent deletions
			for (int idx = oldArray.Length - 1; idx >= 0; idx--)
			{
				int oldHvo = oldArray[idx];
				if (newHvos.Contains(oldHvo))
					combinedHvos.Add(oldHvo);
				else
					data.Replace(hvo, flid, idx, idx + 1, null, 0); // Important to pass *both* null *and* 0 here to remove items
			}

			// Step 2: Add any objects from the "new" list that weren't in the "old" list
			foreach (int newHvo in newArray)
			{
				if (combinedHvos.Contains(newHvo))
					continue;
				// This item was added in the new list
				data.Replace(hvo, flid, combinedHvos.Count, combinedHvos.Count, new int[] { newHvo }, 1);
				combinedHvos.Add(newHvo);
			}
			return true;
		}

		/// <summary>
		/// Return a name suitable for logging from an entry
		/// </summary>
		/// <returns>The lexeme(s), if present, otherwise something suitable.</returns>
		/// <param name="lfEntry">LF entry we want to write about in the log.</param>
		public static string EntryNameForDebugging(LfLexEntry lfEntry)
		{
			if (lfEntry == null)
				return "<null entry>";
			if (lfEntry.Lexeme == null || lfEntry.Lexeme.Values == null)
				return "<null lexeme>";
			return String.Join(", ", lfEntry.Lexeme.Values.Where(x => x != null && !x.IsEmpty).Select(x => x.Value));
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
		/// <param name="wsManager">WritingSystemManager object from LCM, used to get writing system names from integer IDs.</param>
		/// <param name="metaDataCacheAccessor">Meta data cache accessor from LCM, used to get the integer ID of this field's writing system.</param>
		/// <param name="fallbackWs">Writing system to fall back to if we can't figure it out any other way (usually the default user ws).</param>
		public static BsonValue GetCustomStTextValues(IStText obj, int flid,
			WritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int fallbackWs)
		{
			LfMultiParagraph result = GetCustomStTextValuesAsLfMultiPara(obj, flid, wsManager, metaDataCacheAccessor, fallbackWs);
			return result.ToBsonDocument();
		}

		public static LfMultiParagraph GetCustomStTextValuesAsLfMultiPara(IStText obj, int flid,
			WritingSystemManager wsManager, IFwMetaDataCache metaDataCacheAccessor, int fallbackWs)
		{
			if (obj == null || obj.ParagraphsOS == null || obj.ParagraphsOS.Count == 0) return null;
			var result = new LfMultiParagraph();
			// result.Guid = obj.Guid;  // TODO: See if this would break LF PHP
			result.Paragraphs = obj.ParagraphsOS.OfType<IStTxtPara>().Where(para => para.Contents != null).Select(para => LcmParaToLfPara(para, wsManager)).ToList();
			// StText objects in LCM have a single primary writing system, unlike MultiString or MultiUnicode objects
			int fieldWs = metaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(fallbackWs);
			result.InputSystem = wsStr;
			return result;
		}

		public static void SetCustomStTextValues(IStText lcmStText, IEnumerable<LfParagraph> lfParas, int wsId)
		{
			// Step 1: Delete all LCM paragraphs that are no longer found in LF
			var guidsInLf = new HashSet<Guid>(lfParas.Where(p => p.Guid != null).Select(p => p.Guid.Value));
			var parasToDelete = new HashSet<IStTxtPara>();
			for (int i = 0, count = lcmStText.ParagraphsOS.Count; i < count; i++)
			{
				IStTxtPara para = lcmStText[i];
				if (!guidsInLf.Contains(para.Guid))
					parasToDelete.Add(para);
			}
			foreach (IStTxtPara para in parasToDelete)
			{
				if (para.CanDelete)
					para.Delete();
			}

			// Step 2: Step through LF and LCM paragraphs, adding new paragraphs as needed.
			// (We step through LCM paras *by integer index* so that inserting new paragraphs into LCM will place them in the right location)
			int lcmIdx = 0;
			foreach (LfParagraph lfPara in lfParas)
			{
				IStTxtPara lcmPara;
				if (lcmIdx >= lcmStText.ParagraphsOS.Count)
				{
					// Past the end of existing LCM paras: create new para at end
					lcmPara = lcmStText.AddNewTextPara(lfPara.StyleName);
				}
				else
				{
					lcmPara = lcmStText[lcmIdx];
					if (lcmPara.Guid != lfPara.Guid)
					{
						// A new para was inserted into LF at this point; duplicate that in LCM
						lcmPara = lcmStText.InsertNewTextPara(lcmIdx, lfPara.StyleName);
					}
				}
				lcmPara.Contents = ConvertMongoToLcmTsStrings.SpanStrToTsString(lfPara.Content, wsId, lcmStText.Cache.WritingSystemFactory);
				// It turns out that LCM often has an empty StyleName for the normal, default paragraph style. So in those
				// cases, where we've gotten an empty StyleName in the LfParagraph object, we should NOT change it to be
				// the default paragraph style, as that can cause round-tripping problems.
				#if false
				if (String.IsNullOrEmpty(lfPara.StyleName))
				{
					lfPara.StyleName = defaultParagraphStyle; // NOTE: The "defaultParagraphStyle" parameter has been removed
				}
				#endif
				if (!String.IsNullOrEmpty(lfPara.StyleName)) // Not allowed to set an empty style name on an LCM paragraph
				{
					lcmPara.StyleName = lfPara.StyleName;
				}

				lcmIdx++;
			}
		}

		// Convenience overload
		public static void SetCustomStTextValues(IStText lcmStText, IEnumerable<LfParagraph> lfParas)
		{
			SetCustomStTextValues(lcmStText, lfParas, lcmStText.MainWritingSystem);
		}
	}
}
