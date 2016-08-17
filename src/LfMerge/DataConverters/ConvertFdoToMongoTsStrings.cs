// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.CoreImpl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoTsStrings
	{
		private int[] _wsSearchOrder;

		public ConvertFdoToMongoTsStrings(IEnumerable<int> wsPreferences)
		{
			_wsSearchOrder = wsPreferences.ToArray();
		}

		public ConvertFdoToMongoTsStrings(IEnumerable<ILgWritingSystem> wsPreferences)
		{
			_wsSearchOrder = wsPreferences.Select(ws => ws.Handle).ToArray();
		}

		public ConvertFdoToMongoTsStrings(IEnumerable<string> wsPreferences, IWritingSystemManager wsManager)
		{
			_wsSearchOrder = wsPreferences.Select(wsName => wsManager.GetWsFromStr(wsName)).ToArray();
		}

		public static string SafeTsStringText(ITsString tss)
		{
			if (tss == null)
				return null;
			return tss.Text;
		}

		public static string TextFromTsString(ITsString tss)
		{
			// This will replace SafeTsStringText, and will actually deal with <span> elements

			// Spec: the TsString properties ktptWs (an int property) and ktptNamedStyle (a string property)
			// will be replaced with lang="(wsStr)" and class="styleName_PropValue". Other properties, that
			// LF doesn't process, will be preserved in the class attribute as well, as follows:
			//
			// 1) Take value of string property, replace all space characters with "_SPACE_".
			// 2) Take name of property and turn it into a string (sadly, enum's ToString() won't work here since there are multiple enums with the same int value).
			// 3) Depending on whether it's an int or string prop, use "propi" or "props".
			//
			// Result: class="propi_1_ktptWs_1 props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman"
			//
			// TODO: Write that ordered list in a slightly more structured way now that I've got it all down on screen.

			// TODO: Write this function on Wednesday.
			throw new NotImplementedException();
		}

		internal static string IntPropertyName(int prop)
		{
			// UGH. But we can't use enum.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			// Furthermore, since the TsString functions return an int, not an enum, we have to match it as an int.
			switch (prop)
			{
			case (int)FwTextPropType.ktptWs:
				return "ktptWs";
			case (int)FwTextPropType.ktptItalic:
				return "ktptItalic";
			case (int)FwTextPropType.ktptBold:
				return "ktptBold";
			case (int)FwTextPropType.ktptSuperscript:
				return "ktptSuperscript";
			case (int)FwTextPropType.ktptUnderline:
				return "ktptUnderline";
			case (int)FwTextPropType.ktptFontSize:
				return "ktptFontSize";
			case (int)FwTextPropType.ktptOffset:
				return "ktptOffset";
			case (int)FwTextPropType.ktptForeColor:
				return "ktptForeColor";
			case (int)FwTextPropType.ktptBackColor:
				return "ktptBackColor";
			case (int)FwTextPropType.ktptUnderColor:
				return "ktptUnderColor";
			case (int)FwTextPropType.ktptBaseWs:
				return "ktptBaseWs";
			case (int)FwTextPropType.ktptAlign:
				return "ktptAlign";
			case (int)FwTextPropType.ktptFirstIndent:
				return "ktptFirstIndent";
			case (int)FwTextPropType.ktptLeadingIndent:
				return "ktptLeadingIndent";
			case (int)FwTextPropType.ktptTrailingIndent:
				return "ktptTrailingIndent";
			case (int)FwTextPropType.ktptSpaceBefore:
				return "ktptSpaceBefore";
			case (int)FwTextPropType.ktptSpaceAfter:
				return "ktptSpaceAfter";
			case (int)FwTextPropType.ktptTabDef:
				return "ktptTabDef";
			case (int)FwTextPropType.ktptLineHeight:
				return "ktptLineHeight";
			case (int)FwTextPropType.ktptParaColor:
				return "ktptParaColor";
			case (int)FwTextPropType.ktptSpellCheck:
				return "ktptSpellCheck";
			case (int)FwTextPropType.ktptMarginTop:
				return "ktptMarginTop";
			case (int)FwTextPropType.ktptRightToLeft:
				return "ktptRightToLeft";
			case (int)FwTextPropType.ktptDirectionDepth:
				return "ktptDirectionDepth";
			case (int)FwTextPropType.ktptPadLeading:
				return "ktptPadLeading";
			case (int)FwTextPropType.ktptPadTrailing:
				return "ktptPadTrailing";
			case (int)FwTextPropType.ktptPadTop:
				return "ktptPadTop";
			case (int)FwTextPropType.ktptPadBottom:
				return "ktptPadBottom";
			case (int)FwTextPropType.ktptBorderTop:
				return "ktptBorderTop";
			case (int)FwTextPropType.ktptBorderBottom:
				return "ktptBorderBottom";
			case (int)FwTextPropType.ktptBorderLeading:
				return "ktptBorderLeading";
			case (int)FwTextPropType.ktptBorderTrailing:
				return "ktptBorderTrailing";
			case (int)FwTextPropType.ktptBorderColor:
				return "ktptBorderColor";
			case (int)FwTextPropType.ktptBulNumScheme:
				return "ktptBulNumScheme";
			case (int)FwTextPropType.ktptBulNumStartAt:
				return "ktptBulNumStartAt";
			case (int)FwTextPropType.ktptKeepWithNext:
				return "ktptKeepWithNext";
			case (int)FwTextPropType.ktptKeepTogether:
				return "ktptKeepTogether";
			case (int)FwTextPropType.ktptHyphenate:
				return "ktptHyphenate";
			case (int)FwTextPropType.ktptMaxLines:
				return "ktptMaxLines";
			case (int)FwTextPropType.ktptCellBorderWidth:
				return "ktptCellBorderWidth";
			case (int)FwTextPropType.ktptCellSpacing:
				return "ktptCellSpacing";
			case (int)FwTextPropType.ktptCellPadding:
				return "ktptCellPadding";
			case (int)FwTextPropType.ktptEditable:
				return "ktptEditable";
			case (int)FwTextPropType.ktptSetRowDefaults:
				return "ktptSetRowDefaults";
			case (int)FwTextPropType.ktptRelLineHeight:
				return "ktptRelLineHeight";
			case (int)FwTextPropType.ktptTableRule:
				return "ktptTableRule";
			case (int)FwTextPropType.ktptWidowOrphanControl:
				return "ktptWidowOrphanControl";
			case (int)FwTextPropType.ktptMarkItem:
				return "ktptMarkItem";
			default:
				return "ktptUnknownIntProperty";
			}
		}

		internal static string StringPropertyName(int prop)
		{
			// UGH. But we can't use enum.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			// Furthermore, since the TsString functions return an int, not an enum, we have to match it as an int.
			switch (prop)
			{
			case (int)FwTextPropType.ktptFontFamily:
				return "ktptFontFamily";
			case (int)FwTextPropType.ktptCharStyle:
				return "ktptCharStyle";
			case (int)FwTextPropType.ktptParaStyle:
				return "ktptParaStyle";
			case (int)FwTextPropType.ktptTabList:
				return "ktptTabList";
			case (int)FwTextPropType.ktptTags:
				return "ktptTags";
			case (int)FwTextPropType.ktptObjData:
				return "ktptObjData";
			case (int)FwTextPropType.ktptFontVariations:
				return "ktptFontVariations";
			case (int)FwTextPropType.ktptNamedStyle:
				return "ktptNamedStyle";  // We handle this one specially, but keep it in the switch statement anyway
			case (int)FwTextPropType.ktptBulNumTxtBef:
				return "ktptBulNumTxtBef";
			case (int)FwTextPropType.ktptBulNumTxtAft:
				return "ktptBulNumTxtAft";
			case (int)FwTextPropType.ktptBulNumFontInfo:
				return "ktptBulNumFontInfo";
			case (int)FwTextPropType.ktptWsStyle:
				return "ktptWsStyle";
			case (int)FwTextPropType.ktptFieldName:
				return "ktptFieldName";
			default:
				return "ktptUnknownStringProperty";
			}
		}

		public string BestString(IMultiAccessorBase multiString)
		{
			// If this is an IMultiStringAccessor, we can just hand it off to GetBestAlternative
			var accessor = multiString as IMultiStringAccessor;
			if (accessor != null)
			{
				int wsActual;
				return SafeTsStringText(accessor.GetBestAlternative(out wsActual, _wsSearchOrder));
			}
			// JUst a MultiAccessorBase? Then search manually
			string result;
			foreach (int wsId in _wsSearchOrder)
			{
				result = SafeTsStringText(multiString.StringOrNull(wsId));
				if (result != null)
					return result;
			}
			return null;
		}
	}
}

