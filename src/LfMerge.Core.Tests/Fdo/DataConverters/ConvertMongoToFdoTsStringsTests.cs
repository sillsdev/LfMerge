// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.Core.DataConverters;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.Core.Tests.Fdo.DataConverters
{
	public class ConvertMongoToFdoTsStringsTests : FdoTestBase
	{
		// *****************
		//     Test data
		// *****************
		private string zeroSpans = "fooσπιθαμήbarportéebaz";
		private string twoLangs  = "foo<span lang=\"grc\">σπιθ<αμή</span>bar<span lang=\"fr\">port>ée</span>baz";
		private string twoStyles = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"styleName_Default_SPACE_Paragraph_SPACE_Style\">italic</span> text";
		private string twoGuids  = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, but no language spans";
		private string oneGuidOneStyle  = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">guid-containing</span> text";
		private string twoGuidsOneStyle = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, and the first is bold, but there are no language spans";
		private string twoGuidsTwoStylesNoLangs  = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B)</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Default_SPACE_Paragraph_SPACE_Style\">guid (I)</span> classes, and two styles, but there are no language spans";
		private string twoGuidsTwoStylesOneLang  = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B)</span> different <span lang=\"fr\" class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Default_SPACE_Paragraph_SPACE_Style\">guid (I,fr)</span> classes, and two styles, and one language span";
		private string twoGuidsTwoStylesTwoLangs = "this has <span lang=\"grc\" class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B,grc)</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Default_SPACE_Paragraph_SPACE_Style\" lang=\"fr\">guid (I,fr)</span> classes, and two styles, and two language spans";
		private string twoStylesTwoLangsOneOtherProperty   = "this has <span lang=\"grc\" class=\"styleName_Bold\">two</span> different <span class=\"propi_4_ktptSuperscript_1_0 styleName_Default_SPACE_Paragraph_SPACE_Style\" lang=\"fr\">spans</span>, two styles, and two language spans -- and one extra int property";
		private string twoStylesTwoLangsTwoOtherProperties = "this has <span lang=\"grc\" class=\"styleName_Bold\">two (B,grc)</span> different <span class=\"propi_4_ktptSuperscript_1_0 props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman styleName_Default_SPACE_Paragraph_SPACE_Style\" lang=\"fr\">spans</span>, two styles, and two language spans -- and two extra properties, one int and one str";

		private string twoStylesTwoLangsTwoOtherPropertiesEscaped = "this has &lt;span lang=\"grc\" class=\"styleName_Bold\"&gt;two (B,grc)&lt;/span&gt; different &lt;span class=\"propi_4_ktptSuperscript_1_0 props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman styleName_Default_SPACE_Paragraph_SPACE_Style\" lang=\"fr\"&gt;spans&lt;/span&gt;, two styles, and two language spans -- and two extra properties, one int and one str";

		private string containsAngleBrackets = "strings with <angle brackets> need to be HTML-escaped";
		private string containsAngleBracketsEscaped = "strings with &lt;angle brackets&gt; need to be HTML-escaped";
		private string containsHtml = "especially if they would be <script type=\"text/javascript\">alert('security holes');</script>...";
		private string containsHtmlEscaped = "especially if they would be &lt;script type=\"text/javascript\"&gt;alert('security holes');&lt;/script&gt;...";

		private Guid firstGuid  = Guid.Parse("01234567-1234-4321-89ab-0123456789ab");
		private Guid secondGuid = Guid.Parse("98765432-1234-4321-89ab-0123456789ab");

		public ConvertMongoToFdoTsStringsTests()
		{
		}

		// *************
		//     Tests
		// *************
		[Test]
		public void CanDetectSpans_ZeroSpans()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(zeroSpans), Is.EqualTo(0));
		}

		[Test]
		public void CanDetectSpans_TwoLangs()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoLangs), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoStyles()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoStyles), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoGuids()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuids), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_OneGuidOneStyle()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(oneGuidOneStyle), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoGuidsOneStyle()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsOneStyle), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoGuidsTwoStylesNoLangs()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesNoLangs), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoGuidsTwoStylesOneLang()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesOneLang), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoGuidsTwoStylesTwoLangs()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesTwoLangs), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoStylesTwoLangsOneOtherProperty), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoStylesTwoLangsTwoOtherProperties), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoStylesTwoLangsTwoOtherPropertiesEscaped), Is.EqualTo(2));
		}

		[Test]
		public void CanDetectSpans_ContainsAngleBrackets()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(containsAngleBrackets), Is.EqualTo(0));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(containsAngleBracketsEscaped), Is.EqualTo(0));
		}

		[Test]
		public void CanDetectSpans_ContainsHtml()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(containsHtml), Is.EqualTo(0));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(containsHtmlEscaped), Is.EqualTo(0));
		}

		[Test]
		public void CanExtractTextInsideSpans_ZeroSpans()
		{
			string[] textInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanTexts(zeroSpans).ToArray();
			Assert.That(textInZeroSpans.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoLangs()
		{
			string[] textInTwoLangs = ConvertMongoToFdoTsStrings.GetSpanTexts(twoLangs).ToArray();
			Assert.That(textInTwoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoLangs[0], Is.EqualTo("σπιθ<αμή"));
			Assert.That(textInTwoLangs[1], Is.EqualTo("port>ée"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoStyles()
		{
			string[] textInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanTexts(twoStyles).ToArray();
			Assert.That(textInTwoStyles.Length, Is.EqualTo(2));
			Assert.That(textInTwoStyles[0], Is.EqualTo("bold"));
			Assert.That(textInTwoStyles[1], Is.EqualTo("italic"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoGuids()
		{
			string[] textInTwoGuids = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuids).ToArray();
			Assert.That(textInTwoGuids.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuids[0], Is.EqualTo("two"));
			Assert.That(textInTwoGuids[1], Is.EqualTo("guid"));
		}

		[Test]
		public void CanExtractTextInsideSpans_OneGuidOneStyle()
		{
			string[] textInOneGuidOneStyle = ConvertMongoToFdoTsStrings.GetSpanTexts(oneGuidOneStyle).ToArray();
			Assert.That(textInOneGuidOneStyle.Length, Is.EqualTo(2));
			Assert.That(textInOneGuidOneStyle[0], Is.EqualTo("bold"));
			Assert.That(textInOneGuidOneStyle[1], Is.EqualTo("guid-containing"));
		}
		[Test]
		public void CanExtractTextInsideSpans_TwoGuidsOneStyle()
		{
			string[] textInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsOneStyle).ToArray();
			Assert.That(textInTwoGuidsOneStyle.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsOneStyle[0], Is.EqualTo("two"));
			Assert.That(textInTwoGuidsOneStyle[1], Is.EqualTo("guid"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoGuidsTwoStylesNoLangs()
		{
			string[] textInTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesNoLangs).ToArray();
			Assert.That(textInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesNoLangs[0], Is.EqualTo("two (B)"));
			Assert.That(textInTwoGuidsTwoStylesNoLangs[1], Is.EqualTo("guid (I)"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoGuidsTwoStylesOneLang()
		{
			string[] textInTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesOneLang).ToArray();
			Assert.That(textInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesOneLang[0], Is.EqualTo("two (B)"));
			Assert.That(textInTwoGuidsTwoStylesOneLang[1], Is.EqualTo("guid (I,fr)"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoGuidsTwoStylesTwoLangs()
		{
			string[] textInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesTwoLangs).ToArray();
			Assert.That(textInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("two (B,grc)"));
			Assert.That(textInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("guid (I,fr)"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			string[] textInTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.GetSpanTexts(twoStylesTwoLangsOneOtherProperty).ToArray();
			Assert.That(textInTwoStylesTwoLangsOneOtherProperty.Length, Is.EqualTo(2));
			Assert.That(textInTwoStylesTwoLangsOneOtherProperty[0], Is.EqualTo("two"));
			Assert.That(textInTwoStylesTwoLangsOneOtherProperty[1], Is.EqualTo("spans"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			string[] textInTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.GetSpanTexts(twoStylesTwoLangsTwoOtherProperties).ToArray();
			Assert.That(textInTwoStylesTwoLangsTwoOtherProperties.Length, Is.EqualTo(2));
			Assert.That(textInTwoStylesTwoLangsTwoOtherProperties[0], Is.EqualTo("two (B,grc)"));
			Assert.That(textInTwoStylesTwoLangsTwoOtherProperties[1], Is.EqualTo("spans"));
		}

		[Test]
		public void CanExtractTextInsideSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			string[] textInTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.GetSpanTexts(twoStylesTwoLangsTwoOtherPropertiesEscaped).ToArray();
			Assert.That(textInTwoStylesTwoLangsTwoOtherPropertiesEscaped.Length, Is.EqualTo(2));
			Assert.That(textInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0], Is.EqualTo("two (B,grc)"));
			Assert.That(textInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1], Is.EqualTo("spans"));
		}

		[Test]
		public void CanExtractTextInsideSpans_ContainsAngleBrackets()
		{
			string[] textInContainsAngleBrackets = ConvertMongoToFdoTsStrings.GetSpanTexts(containsAngleBrackets).ToArray();
			string[] textInContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.GetSpanTexts(containsAngleBracketsEscaped).ToArray();
			Assert.That(textInContainsAngleBrackets.Length, Is.EqualTo(0));
			Assert.That(textInContainsAngleBracketsEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractTextInsideSpans_ContainsHtml()
		{
			string[] textInContainsHtml = ConvertMongoToFdoTsStrings.GetSpanTexts(containsHtml).ToArray();
			string[] textInContainsHtmlEscaped = ConvertMongoToFdoTsStrings.GetSpanTexts(containsHtmlEscaped).ToArray();
			Assert.That(textInContainsHtml.Length, Is.EqualTo(0));
			Assert.That(textInContainsHtmlEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_ZeroSpans()
		{
			string[] langsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanLanguages(zeroSpans).ToArray();
			Assert.That(langsInZeroSpans.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoLangs()
		{
			string[] langsInTwoLangs = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoLangs).ToArray();
			Assert.That(langsInTwoLangs.Length, Is.EqualTo(2));
			Assert.That(langsInTwoLangs[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoLangs[1], Is.EqualTo("fr"));
		}
		[Test]
		public void CanClassifySpansByLanguage_TwoStyles()
		{
			string[] langsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoStyles).ToArray();
			Assert.That(langsInTwoStyles.Length, Is.EqualTo(0));
		}
		[Test]
		public void CanClassifySpansByLanguage_TwoGuids()
		{
			string[] langsInTwoGuids = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuids).ToArray();
			Assert.That(langsInTwoGuids.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_OneGuidOneStyle()
		{
			string[] langsInOneGuidOneStyle = ConvertMongoToFdoTsStrings.GetSpanLanguages(oneGuidOneStyle).ToArray();
			Assert.That(langsInOneGuidOneStyle.Length, Is.EqualTo(0));
		}
		[Test]
		public void CanClassifySpansByLanguage_TwoGuidsOneStyle()
		{
			string[] langsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsOneStyle).ToArray();
			Assert.That(langsInTwoGuidsOneStyle.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoGuidsTwoStylesNoLangs()
		{
			string[] langsInTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesNoLangs).ToArray();
			Assert.That(langsInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoGuidsTwoStylesOneLang()
		{
			string[] langsInTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesOneLang).ToArray();
			Assert.That(langsInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(1));
			Assert.That(langsInTwoGuidsTwoStylesOneLang[0], Is.EqualTo("fr"));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoGuidsTwoStylesTwoLangs()
		{
			string[] langsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesTwoLangs).ToArray();
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("fr"));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoStylesTwoLangsOneOtherProperty()
		{
			string[] langsInTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoStylesTwoLangsOneOtherProperty).ToArray();
			Assert.That(langsInTwoStylesTwoLangsOneOtherProperty.Length, Is.EqualTo(2));
			Assert.That(langsInTwoStylesTwoLangsOneOtherProperty[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoStylesTwoLangsOneOtherProperty[1], Is.EqualTo("fr"));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoStylesTwoLangsTwoOtherProperties()
		{
			string[] langsInTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoStylesTwoLangsTwoOtherProperties).ToArray();
			Assert.That(langsInTwoStylesTwoLangsTwoOtherProperties.Length, Is.EqualTo(2));
			Assert.That(langsInTwoStylesTwoLangsTwoOtherProperties[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoStylesTwoLangsTwoOtherProperties[1], Is.EqualTo("fr"));
		}

		[Test]
		public void CanClassifySpansByLanguage_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			string[] langsInTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoStylesTwoLangsTwoOtherPropertiesEscaped).ToArray();
			Assert.That(langsInTwoStylesTwoLangsTwoOtherPropertiesEscaped.Length, Is.EqualTo(2));
			Assert.That(langsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1], Is.EqualTo("fr"));
		}

		[Test]
		public void CanClassifySpansByLanguage_ContainsAngleBrackets()
		{
			string[] langsInContainsAngleBrackets = ConvertMongoToFdoTsStrings.GetSpanLanguages(containsAngleBrackets).ToArray();
			string[] langsInContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.GetSpanLanguages(containsAngleBracketsEscaped).ToArray();
			Assert.That(langsInContainsAngleBrackets.Length, Is.EqualTo(0));
			Assert.That(langsInContainsAngleBracketsEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanClassifySpansByLanguage_ContainsHtml()
		{
			string[] langsInContainsHtml = ConvertMongoToFdoTsStrings.GetSpanLanguages(containsHtml).ToArray();
			string[] langsInContainsHtmlEscaped = ConvertMongoToFdoTsStrings.GetSpanLanguages(containsHtmlEscaped).ToArray();
			Assert.That(langsInContainsHtml.Length, Is.EqualTo(0));
			Assert.That(langsInContainsHtmlEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_ZeroSpans()
		{
			Guid[] guidsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanGuids(zeroSpans).ToArray();
			Assert.That(guidsInZeroSpans.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoLangs()
		{
			Guid[] guidsInTwoLangs = ConvertMongoToFdoTsStrings.GetSpanGuids(twoLangs).ToArray();
			Assert.That(guidsInTwoLangs.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoStyles()
		{
			Guid[] guidsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanGuids(twoStyles).ToArray();
			Assert.That(guidsInTwoStyles.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoGuids()
		{
			Guid[] guidsInTwoGuids = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuids).ToArray();
			Assert.That(guidsInTwoGuids.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuids[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuids[1], Is.EqualTo(secondGuid));
		}

		[Test]
		public void CanExtractGuidsFromSpans_OneGuidOneStyle()
		{
			Guid[] guidsInOneGuidOneStyle = ConvertMongoToFdoTsStrings.GetSpanGuids(oneGuidOneStyle).ToArray();
			Assert.That(guidsInOneGuidOneStyle.Length, Is.EqualTo(1));
			Assert.That(guidsInOneGuidOneStyle[0], Is.EqualTo(firstGuid));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoGuidsOneStyle()
		{
			Guid[] guidsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsOneStyle).ToArray();
			Assert.That(guidsInTwoGuidsOneStyle.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsOneStyle[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsOneStyle[1], Is.EqualTo(secondGuid));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoGuidsTwoStylesNoLangs()
		{
			Guid[] guidsInTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesNoLangs).ToArray();
			Assert.That(guidsInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesNoLangs[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesNoLangs[1], Is.EqualTo(secondGuid));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoGuidsTwoStylesOneLang()
		{
			Guid[] guidsInTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesOneLang).ToArray();
			Assert.That(guidsInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesOneLang[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesOneLang[1], Is.EqualTo(secondGuid));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoGuidsTwoStylesTwoLangs()
		{
			Guid[] guidsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesTwoLangs).ToArray();
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo(secondGuid));
		}

		public void CanExtractGuidsFromSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			Guid[] guidsInTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.GetSpanGuids(twoStylesTwoLangsOneOtherProperty).ToArray();
			Assert.That(guidsInTwoStylesTwoLangsOneOtherProperty.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			Guid[] guidsInTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.GetSpanGuids(twoStylesTwoLangsTwoOtherProperties).ToArray();
			Assert.That(guidsInTwoStylesTwoLangsTwoOtherProperties.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			Guid[] guidsInTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.GetSpanGuids(twoStylesTwoLangsTwoOtherPropertiesEscaped).ToArray();
			Assert.That(guidsInTwoStylesTwoLangsTwoOtherPropertiesEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_ContainsAngleBrackets()
		{
			Guid[] guidsInContainsAngleBrackets = ConvertMongoToFdoTsStrings.GetSpanGuids(containsAngleBrackets).ToArray();
			Guid[] guidsInContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.GetSpanGuids(containsAngleBracketsEscaped).ToArray();
			Assert.That(guidsInContainsAngleBrackets.Length, Is.EqualTo(0));
			Assert.That(guidsInContainsAngleBracketsEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractGuidsFromSpans_ContainsHtml()
		{
			Guid[] guidsInContainsHtml = ConvertMongoToFdoTsStrings.GetSpanGuids(containsHtml).ToArray();
			Guid[] guidsInContainsHtmlEscaped = ConvertMongoToFdoTsStrings.GetSpanGuids(containsHtmlEscaped).ToArray();
			Assert.That(guidsInContainsHtml.Length, Is.EqualTo(0));
			Assert.That(guidsInContainsHtmlEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractStylesFromSpans_ZeroSpans()
		{
			string[] stylesInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanStyles(zeroSpans).ToArray();
			Assert.That(stylesInZeroSpans.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoLangs()
		{
			string[] stylesInTwoLangs = ConvertMongoToFdoTsStrings.GetSpanStyles(twoLangs).ToArray();
			Assert.That(stylesInTwoLangs.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoStyles()
		{
			string[] stylesInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanStyles(twoStyles).ToArray();
			Assert.That(stylesInTwoStyles.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoStyles[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoStyles[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoGuids()
		{
			string[] stylesInTwoGuids = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuids).ToArray();
			Assert.That(stylesInTwoGuids.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractStylesFromSpans_OneGuidOneStyle()
		{
			string[] stylesInOneGuidOneStyle = ConvertMongoToFdoTsStrings.GetSpanStyles(oneGuidOneStyle).ToArray();
			Assert.That(stylesInOneGuidOneStyle.Length, Is.EqualTo(1));
			Assert.That(stylesInOneGuidOneStyle[0], Is.EqualTo("Bold"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoGuidsOneStyle()
		{
			string[] stylesInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsOneStyle).ToArray();
			Assert.That(stylesInTwoGuidsOneStyle.Length, Is.EqualTo(1));
			Assert.That(stylesInTwoGuidsOneStyle[0], Is.EqualTo("Bold"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoGuidsTwoStylesNoLangs()
		{
			string[] stylesInTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesNoLangs).ToArray();
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoGuidsTwoStylesOneLang()
		{
			string[] stylesInTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesOneLang).ToArray();
			Assert.That(stylesInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoGuidsTwoStylesOneLang[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesOneLang[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoGuidsTwoStylesTwoLangs()
		{
			string[] stylesInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesTwoLangs).ToArray();
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			string[] stylesInTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.GetSpanStyles(twoStylesTwoLangsOneOtherProperty).ToArray();
			Assert.That(stylesInTwoStylesTwoLangsOneOtherProperty.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoStylesTwoLangsOneOtherProperty[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoStylesTwoLangsOneOtherProperty[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			string[] stylesInTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.GetSpanStyles(twoStylesTwoLangsTwoOtherProperties).ToArray();
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherProperties.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherProperties[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherProperties[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			string[] stylesInTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.GetSpanStyles(twoStylesTwoLangsTwoOtherPropertiesEscaped).ToArray();
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherPropertiesEscaped.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1], Is.EqualTo("Default Paragraph Style"));
		}

		[Test]
		public void CanExtractStylesFromSpans_ContainsAngleBrackets()
		{
			string[] stylesInContainsAngleBrackets = ConvertMongoToFdoTsStrings.GetSpanStyles(containsAngleBrackets).ToArray();
			string[] stylesInContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.GetSpanStyles(containsAngleBracketsEscaped).ToArray();
			Assert.That(stylesInContainsAngleBrackets.Length, Is.EqualTo(0));
			Assert.That(stylesInContainsAngleBracketsEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractStylesFromSpans_ContainsHtml()
		{
			string[] stylesInContainsHtml = ConvertMongoToFdoTsStrings.GetSpanStyles(containsHtml).ToArray();
			string[] stylesInContainsHtmlEscaped = ConvertMongoToFdoTsStrings.GetSpanStyles(containsHtmlEscaped).ToArray();
			Assert.That(stylesInContainsHtml.Length, Is.EqualTo(0));
			Assert.That(stylesInContainsHtmlEscaped.Length, Is.EqualTo(0));
		}

		[Test]
		public void CanExtractRunsFromSpans_ZeroSpans()
		{
			Run[] runsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanRuns(zeroSpans).ToArray();
			Assert.That(runsInZeroSpans.Length, Is.EqualTo(1));
			Assert.That(runsInZeroSpans[0].Content,   Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(runsInZeroSpans[0].Lang,      Is.Null);
			Assert.That(runsInZeroSpans[0].Guid,      Is.Null);
			Assert.That(runsInZeroSpans[0].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoLangs()
		{
			Run[] runsInTwoLangs = ConvertMongoToFdoTsStrings.GetSpanRuns(twoLangs).ToArray();
			Assert.That(runsInTwoLangs.Length, Is.EqualTo(5));
			Assert.That(runsInTwoLangs[0].Content,   Is.EqualTo("foo"));
			Assert.That(runsInTwoLangs[0].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[0].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[0].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[1].Content,   Is.EqualTo("σπιθ<αμή"));
			Assert.That(runsInTwoLangs[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoLangs[1].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[1].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[2].Content,   Is.EqualTo("bar"));
			Assert.That(runsInTwoLangs[2].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[2].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[2].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[3].Content,   Is.EqualTo("port>ée"));
			Assert.That(runsInTwoLangs[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoLangs[3].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[3].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[4].Content,   Is.EqualTo("baz"));
			Assert.That(runsInTwoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoStyles()
		{
			Run[] runsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanRuns(twoStyles).ToArray();
			Assert.That(runsInTwoStyles.Length, Is.EqualTo(5));
			Assert.That(runsInTwoStyles[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoStyles[0].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[0].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[0].StyleName, Is.Null);
			Assert.That(runsInTwoStyles[1].Content,   Is.EqualTo("bold"));
			Assert.That(runsInTwoStyles[1].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[1].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoStyles[2].Content,   Is.EqualTo(" and "));
			Assert.That(runsInTwoStyles[2].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[2].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[2].StyleName, Is.Null);
			Assert.That(runsInTwoStyles[3].Content,   Is.EqualTo("italic"));
			Assert.That(runsInTwoStyles[3].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[3].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoStyles[4].Content,   Is.EqualTo(" text"));
			Assert.That(runsInTwoStyles[4].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[4].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoGuids()
		{
			Run[] runsInTwoGuids = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuids).ToArray();
			Assert.That(runsInTwoGuids.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuids[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoGuids[0].Lang,      Is.Null);
			Assert.That(runsInTwoGuids[0].Guid,      Is.Null);
			Assert.That(runsInTwoGuids[0].StyleName, Is.Null);
			Assert.That(runsInTwoGuids[1].Content,   Is.EqualTo("two"));
			Assert.That(runsInTwoGuids[1].Lang,      Is.Null);
			Assert.That(runsInTwoGuids[1].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuids[1].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInTwoGuids[1].StyleName, Is.Null);
			Assert.That(runsInTwoGuids[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoGuids[2].Lang,      Is.Null);
			Assert.That(runsInTwoGuids[2].Guid,      Is.Null);
			Assert.That(runsInTwoGuids[2].StyleName, Is.Null);
			Assert.That(runsInTwoGuids[3].Content,   Is.EqualTo("guid"));
			Assert.That(runsInTwoGuids[3].Lang,      Is.Null);
			Assert.That(runsInTwoGuids[3].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuids[3].Guid,      Is.EqualTo(secondGuid));
			Assert.That(runsInTwoGuids[3].StyleName, Is.Null);
			Assert.That(runsInTwoGuids[4].Content,   Is.EqualTo(" classes, but no language spans"));
			Assert.That(runsInTwoGuids[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuids[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuids[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_OneGuidOneStyle()
		{
			Run[] runsInOneGuidOneStyle = ConvertMongoToFdoTsStrings.GetSpanRuns(oneGuidOneStyle).ToArray();
			Assert.That(runsInOneGuidOneStyle.Length, Is.EqualTo(5));
			Assert.That(runsInOneGuidOneStyle[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInOneGuidOneStyle[0].Lang,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[0].Guid,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[0].StyleName, Is.Null);
			Assert.That(runsInOneGuidOneStyle[1].Content,   Is.EqualTo("bold"));
			Assert.That(runsInOneGuidOneStyle[1].Lang,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[1].Guid,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInOneGuidOneStyle[2].Content,   Is.EqualTo(" and "));
			Assert.That(runsInOneGuidOneStyle[2].Lang,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[2].Guid,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[2].StyleName, Is.Null);
			Assert.That(runsInOneGuidOneStyle[3].Content,   Is.EqualTo("guid-containing"));
			Assert.That(runsInOneGuidOneStyle[3].Lang,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[3].Guid,      Is.Not.Null);
			Assert.That(runsInOneGuidOneStyle[3].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInOneGuidOneStyle[3].StyleName, Is.Null);
			Assert.That(runsInOneGuidOneStyle[4].Content,   Is.EqualTo(" text"));
			Assert.That(runsInOneGuidOneStyle[4].Lang,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[4].Guid,      Is.Null);
			Assert.That(runsInOneGuidOneStyle[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoGuidsOneStyle()
		{
			Run[] runsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsOneStyle).ToArray();
			Assert.That(runsInTwoGuidsOneStyle.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuidsOneStyle[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoGuidsOneStyle[0].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[0].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[0].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[1].Content,   Is.EqualTo("two"));
			Assert.That(runsInTwoGuidsOneStyle[1].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[1].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsOneStyle[1].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInTwoGuidsOneStyle[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoGuidsOneStyle[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoGuidsOneStyle[2].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[2].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[2].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[3].Content,   Is.EqualTo("guid"));
			Assert.That(runsInTwoGuidsOneStyle[3].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[3].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsOneStyle[3].Guid,      Is.EqualTo(secondGuid));
			Assert.That(runsInTwoGuidsOneStyle[3].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[4].Content,   Is.EqualTo(" classes, and the first is bold, but there are no language spans"));
			Assert.That(runsInTwoGuidsOneStyle[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsOneStyle[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoGuidsTwoStylesNoLangs()
		{
			Run[] runsInTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesNoLangs).ToArray();
			Assert.That(runsInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[0].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[0].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[0].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[1].Content,   Is.EqualTo("two (B)"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[1].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[1].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[1].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[2].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[2].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[2].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].Content,   Is.EqualTo("guid (I)"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].Guid,      Is.EqualTo(secondGuid));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Content,   Is.EqualTo(" classes, and two styles, but there are no language spans"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoGuidsTwoStylesOneLang()
		{
			Run[] runsInTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesOneLang).ToArray();
			Assert.That(runsInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[0].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[0].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[0].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[1].Content,   Is.EqualTo("two (B)"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[1].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[1].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[1].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[2].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[2].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[2].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].Content,   Is.EqualTo("guid (I,fr)"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].Guid,      Is.EqualTo(secondGuid));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Content,   Is.EqualTo(" classes, and two styles, and one language span"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoGuidsTwoStylesTwoLangs()
		{
			Run[] runsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesTwoLangs).ToArray();
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[0].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[0].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[0].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[1].Content,   Is.EqualTo("two (B,grc)"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[1].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[1].Guid,      Is.EqualTo(firstGuid));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[2].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[2].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[2].StyleName, Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].Content,   Is.EqualTo("guid (I,fr)"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].Guid,      Is.Not.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].Guid,      Is.EqualTo(secondGuid));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Content,   Is.EqualTo(" classes, and two styles, and two language spans"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			Run[] runsInTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.GetSpanRuns(twoStylesTwoLangsOneOtherProperty).ToArray();
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty.Length, Is.EqualTo(5));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[0].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[0].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[0].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[1].Content,   Is.EqualTo("two"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[1].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[2].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[2].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[2].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[3].Content,   Is.EqualTo("spans"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[3].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[4].Content,   Is.EqualTo(", two styles, and two language spans -- and one extra int property"));
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[4].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[4].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsOneOtherProperty[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			Run[] runsInTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.GetSpanRuns(twoStylesTwoLangsTwoOtherProperties).ToArray();
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties.Length, Is.EqualTo(5));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[0].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[0].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[0].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[1].Content,   Is.EqualTo("two (B,grc)"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[1].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[2].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[2].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[2].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[3].Content,   Is.EqualTo("spans"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[3].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[4].Content,   Is.EqualTo(", two styles, and two language spans -- and two extra properties, one int and one str"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[4].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[4].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherProperties[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			Run[] runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.GetSpanRuns(twoStylesTwoLangsTwoOtherPropertiesEscaped).ToArray();
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped.Length, Is.EqualTo(5));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0].Content,   Is.EqualTo("this has "));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[0].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1].Content,   Is.EqualTo("two (B,grc)"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[1].StyleName, Is.EqualTo("Bold"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[2].Content,   Is.EqualTo(" different "));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[2].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[2].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[2].StyleName, Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[3].Content,   Is.EqualTo("spans"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[3].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[3].StyleName, Is.EqualTo("Default Paragraph Style"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[4].Content,   Is.EqualTo(", two styles, and two language spans -- and two extra properties, one int and one str"));
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[4].Lang,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[4].Guid,      Is.Null);
			Assert.That(runsInTwoStylesTwoLangsTwoOtherPropertiesEscaped[4].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_ContainsAngleBrackets()
		{
			Run[] runsInContainsAngleBrackets = ConvertMongoToFdoTsStrings.GetSpanRuns(containsAngleBrackets).ToArray();
			Assert.That(runsInContainsAngleBrackets.Length, Is.EqualTo(1));
			Assert.That(runsInContainsAngleBrackets[0].Content,   Is.EqualTo(containsAngleBrackets));
			Assert.That(runsInContainsAngleBrackets[0].Lang,      Is.Null);
			Assert.That(runsInContainsAngleBrackets[0].Guid,      Is.Null);
			Assert.That(runsInContainsAngleBrackets[0].StyleName, Is.Null);
			Run[] runsInContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.GetSpanRuns(containsAngleBracketsEscaped).ToArray();
			Assert.That(runsInContainsAngleBracketsEscaped.Length, Is.EqualTo(1));
			Assert.That(runsInContainsAngleBracketsEscaped[0].Content,   Is.EqualTo(containsAngleBrackets));
			Assert.That(runsInContainsAngleBracketsEscaped[0].Lang,      Is.Null);
			Assert.That(runsInContainsAngleBracketsEscaped[0].Guid,      Is.Null);
			Assert.That(runsInContainsAngleBracketsEscaped[0].StyleName, Is.Null);
		}

		[Test]
		public void CanExtractRunsFromSpans_ContainsHtml()
		{
			Run[] runsInContainsHtml = ConvertMongoToFdoTsStrings.GetSpanRuns(containsHtml).ToArray();
			Assert.That(runsInContainsHtml.Length, Is.EqualTo(1));
			Assert.That(runsInContainsHtml[0].Content,   Is.EqualTo(containsHtml));
			Assert.That(runsInContainsHtml[0].Lang,      Is.Null);
			Assert.That(runsInContainsHtml[0].Guid,      Is.Null);
			Assert.That(runsInContainsHtml[0].StyleName, Is.Null);
			Run[] runsInContainsHtmlEscaped = ConvertMongoToFdoTsStrings.GetSpanRuns(containsHtmlEscaped).ToArray();
			Assert.That(runsInContainsHtmlEscaped.Length, Is.EqualTo(1));
			Assert.That(runsInContainsHtmlEscaped[0].Content,   Is.EqualTo(containsHtml));
			Assert.That(runsInContainsHtmlEscaped[0].Lang,      Is.Null);
			Assert.That(runsInContainsHtmlEscaped[0].Guid,      Is.Null);
			Assert.That(runsInContainsHtmlEscaped[0].StyleName, Is.Null);
		}

		[Test]
		public void CanCreateTsStringsFromSpans_ZeroSpans()
		{
			ITsString tsStrFromZeroSpans = ConvertMongoToFdoTsStrings.SpanStrToTsString(zeroSpans,  _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromZeroSpans, Is.Not.Null);
			Assert.That(tsStrFromZeroSpans.Text, Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(tsStrFromZeroSpans.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromZeroSpans.get_RunText(0), Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(GetStyle(tsStrFromZeroSpans, 0), Is.Null);
			Assert.That(GetWs(tsStrFromZeroSpans, 0), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoLangs()
		{
			ITsString tsStrFromTwoLangs = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoLangs, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoLangs, Is.Not.Null);
			Assert.That(tsStrFromTwoLangs.Text, Is.EqualTo("fooσπιθ<αμήbarport>éebaz"));
			Assert.That(tsStrFromTwoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoLangs.get_RunText(0), Is.EqualTo("foo"));
			Assert.That(tsStrFromTwoLangs.get_RunText(1), Is.EqualTo("σπιθ<αμή"));
			Assert.That(tsStrFromTwoLangs.get_RunText(2), Is.EqualTo("bar"));
			Assert.That(tsStrFromTwoLangs.get_RunText(3), Is.EqualTo("port>ée"));
			Assert.That(tsStrFromTwoLangs.get_RunText(4), Is.EqualTo("baz"));
			Assert.That(GetStyle(tsStrFromTwoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 1), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 3), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 4), Is.Null);
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");
			Assert.That(GetWs(tsStrFromTwoLangs, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoLangs, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoLangs, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoLangs, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoLangs, 4), Is.EqualTo(wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoStyles()
		{
			ITsString tsStrFromTwoStyles = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoStyles, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoStyles, Is.Not.Null);
			Assert.That(tsStrFromTwoStyles.Text, Is.EqualTo("this has bold and italic text"));
			Assert.That(tsStrFromTwoStyles.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoStyles.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoStyles.get_RunText(1), Is.EqualTo("bold"));
			Assert.That(tsStrFromTwoStyles.get_RunText(2), Is.EqualTo(" and "));
			Assert.That(tsStrFromTwoStyles.get_RunText(3), Is.EqualTo("italic"));
			Assert.That(tsStrFromTwoStyles.get_RunText(4), Is.EqualTo(" text"));
			Assert.That(GetStyle(tsStrFromTwoStyles, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStyles, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoStyles, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStyles, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoStyles, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoStyles, 0), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 1), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 2), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 3), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 4), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoGuids()
		{
			ITsString tsStrFromTwoGuids = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuids, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoGuids, Is.Not.Null);
			Assert.That(tsStrFromTwoGuids.Text, Is.EqualTo("this has two different guid classes, but no language spans"));
			Assert.That(tsStrFromTwoGuids.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromTwoGuids.get_RunText(0), Is.EqualTo("this has two different guid classes, but no language spans"));
			Assert.That(GetStyle(tsStrFromTwoGuids, 0), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuids, 0), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_OneGuidOneStyle()
		{
			ITsString tsStrFromOneGuidOneStyle = ConvertMongoToFdoTsStrings.SpanStrToTsString(oneGuidOneStyle, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromOneGuidOneStyle, Is.Not.Null);
			Assert.That(tsStrFromOneGuidOneStyle.Text, Is.EqualTo("this has bold and guid-containing text"));
			Assert.That(tsStrFromOneGuidOneStyle.RunCount, Is.EqualTo(3));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(1), Is.EqualTo("bold"));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(2), Is.EqualTo(" and guid-containing text"));
			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 2), Is.Null);
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 0), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 1), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 2), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoGuidsOneStyle()
		{
			ITsString tsStrFromTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsOneStyle, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoGuidsOneStyle, Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsOneStyle.Text, Is.EqualTo("this has two different guid classes, and the first is bold, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsOneStyle.RunCount, Is.EqualTo(3));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(1), Is.EqualTo("two"));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(2), Is.EqualTo(" different guid classes, and the first is bold, but there are no language spans"));
			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 2), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 0), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 1), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 2), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoGuidsTwoStylesNoLangs()
		{
			ITsString tsStrFromTwoGuidsTwoStylesNoLangs = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesNoLangs, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs, Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.Text, Is.EqualTo("this has two (B) different guid (I) classes, and two styles, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(1), Is.EqualTo("two (B)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(3), Is.EqualTo("guid (I)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(4), Is.EqualTo(" classes, and two styles, but there are no language spans"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 0), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 1), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 2), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 3), Is.EqualTo(_wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 4), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoGuidsTwoStylesOneLang()
		{
			ITsString tsStrFromTwoGuidsTwoStylesOneLang = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesOneLang, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang, Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.Text, Is.EqualTo("this has two (B) different guid (I,fr) classes, and two styles, and one language span"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(1), Is.EqualTo("two (B)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(3), Is.EqualTo("guid (I,fr)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(4), Is.EqualTo(" classes, and two styles, and one language span"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 4), Is.Null);
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 4), Is.EqualTo(wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoGuidsTwoStylesTwoLangs()
		{
			ITsString tsStrFromTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesTwoLangs, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs, Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.Text, Is.EqualTo("this has two (B,grc) different guid (I,fr) classes, and two styles, and two language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(1), Is.EqualTo("two (B,grc)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(3), Is.EqualTo("guid (I,fr)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(4), Is.EqualTo(" classes, and two styles, and two language spans"));
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 4), Is.EqualTo(wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoStylesTwoLangsOneOtherProperty()
		{
			ITsString tsStrFromTwoStylesTwoLangsOneOtherProperty = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoStylesTwoLangsOneOtherProperty, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty, Is.Not.Null);
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.Text, Is.EqualTo("this has two different spans, two styles, and two language spans -- and one extra int property"));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.get_RunText(1), Is.EqualTo("two"));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.get_RunText(3), Is.EqualTo("spans"));
			Assert.That(tsStrFromTwoStylesTwoLangsOneOtherProperty.get_RunText(4), Is.EqualTo(", two styles, and two language spans -- and one extra int property"));
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsOneOtherProperty, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsOneOtherProperty, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsOneOtherProperty, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsOneOtherProperty, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsOneOtherProperty, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsOneOtherProperty, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsOneOtherProperty, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsOneOtherProperty, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsOneOtherProperty, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsOneOtherProperty, 4), Is.EqualTo(wsEn));

			// Extra int property "propi_4_ktptSuperscript_1_0" on second span; first should return "none"
			ITsTextProps props;
			int variation;
			int superscript;
			props = tsStrFromTwoStylesTwoLangsOneOtherProperty.get_Properties(1);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			Assert.That(superscript, Is.EqualTo(-1)); // -1 means "property not found"
			Assert.That(variation, Is.EqualTo(-1));
			// Second span (fourth run, so index 3) should have it
			props = tsStrFromTwoStylesTwoLangsOneOtherProperty.get_Properties(3);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			Assert.That(superscript, Is.EqualTo(1));
			Assert.That(variation, Is.EqualTo(0));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoStylesTwoLangsTwoOtherProperties()
		{
			ITsString tsStrFromTwoStylesTwoLangsTwoOtherProperties = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoStylesTwoLangsTwoOtherProperties, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties, Is.Not.Null);
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.Text, Is.EqualTo("this has two (B,grc) different spans, two styles, and two language spans -- and two extra properties, one int and one str"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_RunText(1), Is.EqualTo("two (B,grc)"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_RunText(3), Is.EqualTo("spans"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_RunText(4), Is.EqualTo(", two styles, and two language spans -- and two extra properties, one int and one str"));
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherProperties, 4), Is.EqualTo(wsEn));

			// Extra int property "propi_4_ktptSuperscript_1_0" on second span; first should return "none"
			// Extra string property "props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman" on second span; first should return "none"
			ITsTextProps props;
			int variation;
			int superscript;
			string fontFamily;
			props = tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_Properties(1);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			fontFamily = props.GetStrPropValue((int)FwTextPropType.ktptFontFamily);
			Assert.That(superscript, Is.EqualTo(-1)); // -1 means "property not found" for int properties
			Assert.That(variation, Is.EqualTo(-1));
			Assert.That(fontFamily, Is.Null); // null means "property not found" for string properties
			// Second span (fourth run, so index 3) should have both properties
			props = tsStrFromTwoStylesTwoLangsTwoOtherProperties.get_Properties(3);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			fontFamily = props.GetStrPropValue((int)FwTextPropType.ktptFontFamily);
			Assert.That(fontFamily, Is.EqualTo("Times New Roman"));
			Assert.That(superscript, Is.EqualTo(1));
			Assert.That(variation, Is.EqualTo(0));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_TwoStylesTwoLangsTwoOtherPropertiesEscaped()
		{
			ITsString tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoStylesTwoLangsTwoOtherPropertiesEscaped, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, Is.Not.Null);
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.Text, Is.EqualTo("this has two (B,grc) different spans, two styles, and two language spans -- and two extra properties, one int and one str"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_RunText(1), Is.EqualTo("two (B,grc)"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_RunText(3), Is.EqualTo("spans"));
			Assert.That(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_RunText(4), Is.EqualTo(", two styles, and two language spans -- and two extra properties, one int and one str"));
			int wsEn = _wsEn;
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 3), Is.EqualTo("Default Paragraph Style"));
			Assert.That(GetStyle(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped, 4), Is.EqualTo(wsEn));

			// Extra int property "propi_4_ktptSuperscript_1_0" on second span; first should return "none"
			// Extra string property "props_1_ktptFontFamily_Times_SPACE_New_SPACE_Roman" on second span; first should return "none"
			ITsTextProps props;
			int variation;
			int superscript;
			string fontFamily;
			props = tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_Properties(1);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			fontFamily = props.GetStrPropValue((int)FwTextPropType.ktptFontFamily);
			Assert.That(superscript, Is.EqualTo(-1)); // -1 means "property not found" for int properties
			Assert.That(variation, Is.EqualTo(-1));
			Assert.That(fontFamily, Is.Null); // null means "property not found" for string properties
			// Second span (fourth run, so index 3) should have both properties
			props = tsStrFromTwoStylesTwoLangsTwoOtherPropertiesEscaped.get_Properties(3);
			superscript = props.GetIntPropValues((int)FwTextPropType.ktptSuperscript, out variation);
			fontFamily = props.GetStrPropValue((int)FwTextPropType.ktptFontFamily);
			Assert.That(fontFamily, Is.EqualTo("Times New Roman"));
			Assert.That(superscript, Is.EqualTo(1));
			Assert.That(variation, Is.EqualTo(0));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_ContainsAngleBrackets()
		{
			ITsString tsStrFromContainsAngleBrackets = ConvertMongoToFdoTsStrings.SpanStrToTsString(containsAngleBrackets, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromContainsAngleBrackets, Is.Not.Null);
			Assert.That(tsStrFromContainsAngleBrackets.Text, Is.EqualTo(containsAngleBrackets));
			Assert.That(tsStrFromContainsAngleBrackets.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromContainsAngleBrackets.get_RunText(0), Is.EqualTo(containsAngleBrackets));
			Assert.That(GetStyle(tsStrFromContainsAngleBrackets, 0), Is.Null);
			Assert.That(GetWs(tsStrFromContainsAngleBrackets, 0), Is.EqualTo(_wsEn));
			ITsString tsStrFromContainsAngleBracketsEscaped = ConvertMongoToFdoTsStrings.SpanStrToTsString(containsAngleBracketsEscaped, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromContainsAngleBracketsEscaped, Is.Not.Null);
			Assert.That(tsStrFromContainsAngleBracketsEscaped.Text, Is.EqualTo(containsAngleBrackets));
			Assert.That(tsStrFromContainsAngleBracketsEscaped.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromContainsAngleBracketsEscaped.get_RunText(0), Is.EqualTo(containsAngleBrackets));
			Assert.That(GetStyle(tsStrFromContainsAngleBracketsEscaped, 0), Is.Null);
			Assert.That(GetWs(tsStrFromContainsAngleBracketsEscaped, 0), Is.EqualTo(_wsEn));
		}

		[Test]
		public void CanCreateTsStringsFromSpans_ContainsHtml()
		{
			ITsString tsStrFromContainsHtml = ConvertMongoToFdoTsStrings.SpanStrToTsString(containsHtml, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromContainsHtml, Is.Not.Null);
			Assert.That(tsStrFromContainsHtml.Text, Is.EqualTo(containsHtml));
			Assert.That(tsStrFromContainsHtml.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromContainsHtml.get_RunText(0), Is.EqualTo(containsHtml));
			Assert.That(GetStyle(tsStrFromContainsHtml, 0), Is.Null);
			Assert.That(GetWs(tsStrFromContainsHtml, 0), Is.EqualTo(_wsEn));
			ITsString tsStrFromContainsHtmlEscaped = ConvertMongoToFdoTsStrings.SpanStrToTsString(containsHtmlEscaped, _wsEn, _cache.WritingSystemFactory);
			Assert.That(tsStrFromContainsHtmlEscaped, Is.Not.Null);
			Assert.That(tsStrFromContainsHtmlEscaped.Text, Is.EqualTo(containsHtml));
			Assert.That(tsStrFromContainsHtmlEscaped.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromContainsHtmlEscaped.get_RunText(0), Is.EqualTo(containsHtml));
			Assert.That(GetStyle(tsStrFromContainsHtmlEscaped, 0), Is.Null);
			Assert.That(GetWs(tsStrFromContainsHtmlEscaped, 0), Is.EqualTo(_wsEn));
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_SingleRunString()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Just English text");
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_StringWithTwoLanguages()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.Append("du texte français");
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_StringWithThreeLanguages()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.Append("du texte français");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("grc"));
			builder.Append("Ελληνικά");
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_StringWithThreeLanguagesAndArbitraryProperties()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.SetIntPropValues((int)FwTextPropType.ktptAlign, (int)FwTextPropVar.ktpvDefault, 2);
			builder.SetStrPropValue((int)FwTextPropType.ktptCharStyle, "Default Character Style");
			builder.SetStrPropValue((int)FwTextPropType.ktptParaStyle, "Default Paragraph Style");
			builder.Append("du texte français");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("grc"));
			builder.SetIntPropValues((int)FwTextPropType.ktptFirstIndent, (int)FwTextPropVar.ktpvMilliPoint, 12000);
			builder.SetStrPropValue((int)FwTextPropType.ktptParaStyle, "Some Other Style");
			builder.Append("Ελληνικά");
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_ContainsAngleBrackets()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsAngleBrackets);
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_ContainsHtml()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsHtml);
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_ContainsAngleBracketsEscaped()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsAngleBracketsEscaped);
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringCanRoundTripFdoToMongoToFdo_ContainsHtmlEscaped()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsHtmlEscaped);
			ITsString tss = builder.GetString();

			// Round-trip
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Compare
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_SingleRunString()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Just English text");
			ITsString tss = builder.GetString();

			string text = "Just English text";

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_StringWithTwoLanguages()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.Append("du texte français");
			ITsString tss = builder.GetString();

			string text = "Some English text<span lang=\"fr\">du texte français</span>";

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_StringWithThreeLanguages()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.Append("du texte français");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("grc"));
			builder.Append("Ελληνικά");
			ITsString tss = builder.GetString();

			string text = "Some English text<span lang=\"fr\">du texte français</span><span lang=\"grc\">Ελληνικά</span>";

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_StringWithThreeLanguagesAndArbitraryProperties()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append("Some English text");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("fr"));
			builder.SetIntPropValues((int)FwTextPropType.ktptAlign, (int)FwTextPropVar.ktpvDefault, 2);
			builder.SetStrPropValue((int)FwTextPropType.ktptCharStyle, "Default Character Style");
			builder.SetStrPropValue((int)FwTextPropType.ktptParaStyle, "Default Paragraph Style");
			builder.Append("du texte français");
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, wsf.GetWsFromStr("grc"));
			builder.SetIntPropValues((int)FwTextPropType.ktptFirstIndent, (int)FwTextPropVar.ktpvMilliPoint, 12000);
			builder.SetStrPropValue((int)FwTextPropType.ktptParaStyle, "Some Other Style");
			builder.Append("Ελληνικά");
			ITsString tss = builder.GetString();

			string text = "Some English text<span lang=\"fr\" class=\"propi_17_ktptAlign_2_0 props_2_ktptCharStyle_Default_SPACE_Character_SPACE_Style props_3_ktptParaStyle_Default_SPACE_Paragraph_SPACE_Style\">du texte français</span><span lang=\"grc\" class=\"propi_17_ktptAlign_2_0 propi_18_ktptFirstIndent_12000_1 props_2_ktptCharStyle_Default_SPACE_Character_SPACE_Style props_3_ktptParaStyle_Some_SPACE_Other_SPACE_Style\">Ελληνικά</span>";

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_ContainsAngleBrackets()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsAngleBrackets);
			ITsString tss = builder.GetString();

			string text = containsAngleBracketsEscaped;

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsCanRoundTripMongoToFdoToMongo_ContainsHtml()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsHtml);
			ITsString tss = builder.GetString();

			string text = containsHtmlEscaped;

			// Round-trip
			ITsString tss2 = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);
			string text2 = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Compare
			Assert.That(text2, Is.EqualTo(text));
			// Also check TsStrings for completion's sake
			TsStringDiffInfo diff = TsStringUtils.GetDiffsInTsStrings(tss, tss2);
			Assert.That(diff, Is.Null);
		}

		[Test]
		public void TsStringsAreHtmlEscapedWhenConvertedToSpanText_ContainsAngleBrackets()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsAngleBrackets);
			ITsString tss = builder.GetString();

			// Exercise
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Verify
			Assert.That(text, Is.EqualTo(containsAngleBracketsEscaped));
		}

		[Test]
		public void TsStringsAreHtmlEscapedWhenConvertedToSpanText_ContainsHtml()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			ITsIncStrBldr builder = TsIncStrBldrClass.Create();
			builder.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, _wsEn);
			builder.Append(containsHtml);
			ITsString tss = builder.GetString();

			// Exercise
			string text = ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsf);

			// Verify
			Assert.That(text, Is.EqualTo(containsHtmlEscaped));
		}

		[Test]
		public void TsStringsAreHtmlUnescapedWhenConvertedBackFromSpanText_ContainsAngleBrackets()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			string text = containsAngleBracketsEscaped;

			// Exercise
			ITsString tss = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Verify
			Assert.That(tss.Text, Is.EqualTo(containsAngleBrackets));
		}

		[Test]
		public void TsStringsAreHtmlUnescapedWhenConvertedBackFromSpanText_ContainsHtml()
		{
			// Setup
			ILgWritingSystemFactory wsf = _cache.WritingSystemFactory;
			string text = containsHtmlEscaped;

			// Exercise
			ITsString tss = ConvertMongoToFdoTsStrings.SpanStrToTsString(text, _wsEn, wsf);

			// Verify
			Assert.That(tss.Text, Is.EqualTo(containsHtml));
		}

		// Helper functions for TsString test
		string GetStyle(ITsString tss, int propNum)
		{
			ITsTextProps prop = tss.get_Properties(propNum);
			return prop.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
		}

		int GetWs(ITsString tss, int propNum)
		{
			int ignored;
			ITsTextProps prop = tss.get_Properties(propNum);
			return prop.GetIntPropValues((int)FwTextPropType.ktptWs, out ignored);
		}
	}
}
