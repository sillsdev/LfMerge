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

		internal static string IntPropertyName(FwTextPropType prop)
		{
			// UGH. But we can't use prop.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			switch (prop)
			{
			case FwTextPropType.ktptWs:
				return "ktptWs";
			case FwTextPropType.ktptItalic:
				return "ktptItalic";
			case FwTextPropType.ktptBold:
				return "ktptBold";
			case FwTextPropType.ktptSuperscript:
				return "ktptSuperscript";
			case FwTextPropType.ktptUnderline:
				return "ktptUnderline";
			case FwTextPropType.ktptFontSize:
				return "ktptFontSize";
			case FwTextPropType.ktptOffset:
				return "ktptOffset";
			case FwTextPropType.ktptForeColor:
				return "ktptForeColor";
			case FwTextPropType.ktptBackColor:
				return "ktptBackColor";
			case FwTextPropType.ktptUnderColor:
				return "ktptUnderColor";
			case FwTextPropType.ktptBaseWs:
				return "ktptBaseWs";
			case FwTextPropType.ktptAlign:
				return "ktptAlign";
			case FwTextPropType.ktptFirstIndent:
				return "ktptFirstIndent";
			case FwTextPropType.ktptLeadingIndent:
				return "ktptLeadingIndent";
			case FwTextPropType.ktptTrailingIndent:
				return "ktptTrailingIndent";
			case FwTextPropType.ktptSpaceBefore:
				return "ktptSpaceBefore";
			case FwTextPropType.ktptSpaceAfter:
				return "ktptSpaceAfter";
			case FwTextPropType.ktptTabDef:
				return "ktptTabDef";
			case FwTextPropType.ktptLineHeight:
				return "ktptLineHeight";
			case FwTextPropType.ktptParaColor:
				return "ktptParaColor";
			case FwTextPropType.ktptSpellCheck:
				return "ktptSpellCheck";
			case FwTextPropType.ktptMarginTop:
				return "ktptMarginTop";
			case FwTextPropType.ktptRightToLeft:
				return "ktptRightToLeft";
			case FwTextPropType.ktptDirectionDepth:
				return "ktptDirectionDepth";
			case FwTextPropType.ktptPadLeading:
				return "ktptPadLeading";
			case FwTextPropType.ktptPadTrailing:
				return "ktptPadTrailing";
			case FwTextPropType.ktptPadTop:
				return "ktptPadTop";
			case FwTextPropType.ktptPadBottom:
				return "ktptPadBottom";
			case FwTextPropType.ktptBorderTop:
				return "ktptBorderTop";
			case FwTextPropType.ktptBorderBottom:
				return "ktptBorderBottom";
			case FwTextPropType.ktptBorderLeading:
				return "ktptBorderLeading";
			case FwTextPropType.ktptBorderTrailing:
				return "ktptBorderTrailing";
			case FwTextPropType.ktptBorderColor:
				return "ktptBorderColor";
			case FwTextPropType.ktptBulNumScheme:
				return "ktptBulNumScheme";
			case FwTextPropType.ktptBulNumStartAt:
				return "ktptBulNumStartAt";
			case FwTextPropType.ktptKeepWithNext:
				return "ktptKeepWithNext";
			case FwTextPropType.ktptKeepTogether:
				return "ktptKeepTogether";
			case FwTextPropType.ktptHyphenate:
				return "ktptHyphenate";
			case FwTextPropType.ktptMaxLines:
				return "ktptMaxLines";
			case FwTextPropType.ktptCellBorderWidth:
				return "ktptCellBorderWidth";
			case FwTextPropType.ktptCellSpacing:
				return "ktptCellSpacing";
			case FwTextPropType.ktptCellPadding:
				return "ktptCellPadding";
			case FwTextPropType.ktptEditable:
				return "ktptEditable";
			case FwTextPropType.ktptSetRowDefaults:
				return "ktptSetRowDefaults";
			case FwTextPropType.ktptRelLineHeight:
				return "ktptRelLineHeight";
			case FwTextPropType.ktptTableRule:
				return "ktptTableRule";
			case FwTextPropType.ktptWidowOrphanControl:
				return "ktptWidowOrphanControl";
			case FwTextPropType.ktptMarkItem:
				return "ktptMarkItem";
			default:
				return "ktptUnknownIntProperty";
			}
		}

		internal static string StringPropertyName(FwTextPropType prop)
		{
			// UGH. But we can't use prop.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			switch (prop)
			{
			case FwTextPropType.ktptFontFamily:
				return "ktptFontFamily";
			case FwTextPropType.ktptCharStyle:
				return "ktptCharStyle";
			case FwTextPropType.ktptParaStyle:
				return "ktptParaStyle";
			case FwTextPropType.ktptTabList:
				return "ktptTabList";
			case FwTextPropType.ktptTags:
				return "ktptTags";
			case FwTextPropType.ktptObjData:
				return "ktptObjData";
			case FwTextPropType.ktptFontVariations:
				return "ktptFontVariations";
			case FwTextPropType.ktptNamedStyle:
				return "ktptNamedStyle";  // We handle this one specially, but keep it in the switch statement anyway
			case FwTextPropType.ktptBulNumTxtBef:
				return "ktptBulNumTxtBef";
			case FwTextPropType.ktptBulNumTxtAft:
				return "ktptBulNumTxtAft";
			case FwTextPropType.ktptBulNumFontInfo:
				return "ktptBulNumFontInfo";
			case FwTextPropType.ktptWsStyle:
				return "ktptWsStyle";
			case FwTextPropType.ktptFieldName:
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

