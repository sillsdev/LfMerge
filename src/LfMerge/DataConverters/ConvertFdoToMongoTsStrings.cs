// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.CoreImpl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoTsStrings
	{
		public static string TextFromTsString(ITsString tss, ILgWritingSystemFactory wsf)
		{
			// This will replace SafeTsStringText, and will actually deal with <span> elements

			// Spec: the TsString properties ktptWs (an int property) and ktptNamedStyle (a string property)
			// will be replaced with lang="(wsStr)" and class="styleName_PropValue". Other properties, that
			// LF doesn't process, will be preserved in the class attribute as well, as follows:
			//
			// 1) Determine whether the property is an int property or a string property.
			// 2) Call either IntPropertyName() or StringPropertyName() to get its name.
			// 3) Create a class string in format "propC_N_NAME_VALUE" where:
			//      - C is "i" for int props or "s" for string props
			//      - N is the property number
			//      - NAME is, of course, the property name
			//      - VALUE is the property's value, with two special cases:
			//          * in string properties, spaces are first replaced by "_SPACE_"
			//          * in int properties, there's a value and a "variation", and VALUE is both of them separated by _
			//
			// Result: class="propi_1_ktptWs_1_0 props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman"

			if (tss == null)
				return null;
			int[] intPropsToSkip = new int[] { (int)FwTextPropType.ktptWs };
			int[] strPropsToSkip = new int[] { (int)FwTextPropType.ktptNamedStyle };
			int mainWs = tss.get_WritingSystem(0);
			var resultBuilder = new StringBuilder();
			for (int i = 0, n = tss.RunCount; i < n; i++)
			{
				string runText = tss.get_RunText(i);
				ITsTextProps props = tss.get_Properties(i);
				// int ignored;
				// int ws = props.GetIntPropValues((int)FwTextPropType.ktptWs, out ignored);
				int ws = tss.get_WritingSystem(i);  // Simpler than GetIntPropValues((int)FwTextPropType.ktptWs)
				string namedStyle = props.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
				List<string> classes = ClassesFromTsTextProps(props, intPropsToSkip, strPropsToSkip);

				bool needSpan = false;
				string langAttr = null;
				string classAttr = null;
				if (ws != mainWs)
				{
					needSpan = true;
					langAttr = String.Format(" lang=\"{0}\"", wsf.GetStrFromWs(ws));
				}
				if (namedStyle != null)
				{
					needSpan = true;
					classes.Insert(0, String.Format("styleName_{0}", namedStyle.Replace(" ", "_SPACE_")));
				}
				if (classes.Count > 0)
				{
					needSpan = true;
					classAttr = String.Format(" class=\"{0}\"", String.Join(" ", classes));
				}

				if (needSpan)
				{
					var spanBuilder = new StringBuilder();
					spanBuilder.Append("<span");
					if (langAttr != null)
						spanBuilder.Append(langAttr);
					if (classAttr != null)
						spanBuilder.Append(classAttr);
					spanBuilder.Append(">");
					spanBuilder.Append(runText);
					spanBuilder.Append("</span>");
					resultBuilder.Append(spanBuilder.ToString());
				}
				else
					resultBuilder.Append(runText);
			}
			string result = resultBuilder.ToString();
			if (String.IsNullOrEmpty(result))
				result = null; // We prefer nulls rather than empty strings
			return result;
		}

		public static List<string> ClassesFromTsTextProps(ITsTextProps props, int[] intPropsToSkip, int[] strPropsToSkip)
		{
			var classes = new List<string>();
			for (int i = 0, n = props.IntPropCount; i < n; i++)
			{
				int propNum;
				int variation;
				int propValue = props.GetIntProp(i, out propNum, out variation);
				if (intPropsToSkip.Contains(propNum))
					continue;
				string className = String.Format("propi_{0}_{1}_{2}_{3}", propNum, IntPropertyName(propNum), propValue, variation);
				classes.Add(className);
			}
			for (int i = 0, n = props.StrPropCount; i < n; i++)
			{
				int propNum;
				string propValue = props.GetStrProp(i, out propNum).Replace(" ", "_SPACE_");
				string className = String.Format("props_{0}_{1}_{2}", propNum, StringPropertyName(propNum), propValue);
				if (strPropsToSkip.Contains(propNum))
					continue;
				classes.Add(className);
			}
			return classes;
		}

		internal static string IntPropertyName(int propNum)
		{
			// UGH. But we can't use enum.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			// Furthermore, since the TsString functions return an int, not an enum, we have to match it as an int.
			switch (propNum)
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

		internal static string StringPropertyName(int propNum)
		{
			// UGH. But we can't use enum.ToString() because the FwTextPropType enum was "overloaded"; that is, there are two
			// different FwTextPropType values with the value 1 (FwTextPropType.ktptWs and FwTextPropType.ktptFontFamily) and
			// the C# spec says you can't count on which one ToString() will return in that case. So we have to do this nonsense.
			// Furthermore, since the TsString functions return an int, not an enum, we have to match it as an int.
			switch (propNum)
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
	}
}

