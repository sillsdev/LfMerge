// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.DataConverters;
using NUnit.Framework;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.Tests.Fdo.DataConverters
{
	public class ConvertMongoToFdoTsStringsTests : FdoTestBase
	{
		// *****************
		//     Test data
		// *****************
		private string noSpans   = "fooσπιθαμήbarportéebaz";
		private string twoLangs  = "foo<span lang=\"grc\">σπιθαμή</span>bar<span lang=\"fr\">portée</span>baz";
		private string twoStyles = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"styleName_Italic\">italic</span> text";
		private string twoGuids  = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, but no language spans";
		private string oneGuidOneStyle  = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">guid-containing</span> text";
		private string twoGuidsOneStyle = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, and the first is bold, but there are no language spans";
		private string twoGuidsTwoStylesNoLangs = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B)</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Italic\">guid (I)</span> classes, and two styles, but there are no language spans";
		private string twoGuidsTwoStylesOneLang = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B)</span> different <span lang=\"fr\" class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Italic\">guid (I,fr)</span> classes, and two styles, and one language span";
		private string twoGuidsTwoStylesTwoLangs = "this has <span lang=\"grc\" class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two (B,grc)</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab styleName_Italic\" lang=\"fr\">guid (I,fr)</span> classes, and two styles, and two language spans";

		private Guid firstGuid  = Guid.Parse("01234567-1234-4321-89ab-0123456789ab");
		private Guid secondGuid = Guid.Parse("98765432-1234-4321-89ab-0123456789ab");

		public ConvertMongoToFdoTsStringsTests()
		{
		}

		// *************
		//     Tests
		// *************
		[Test]
		public void CanDetectSpans()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(noSpans),   Is.EqualTo(0));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoLangs),  Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoStyles), Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuids),  Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(oneGuidOneStyle),  Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsOneStyle), Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesNoLangs),  Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesOneLang),  Is.EqualTo(2));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(twoGuidsTwoStylesTwoLangs), Is.EqualTo(2));
		}

		[Test]
		public void CanExtractTextInsideSpans()
		{
			string[] textInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanTexts(noSpans)  .ToArray();
			string[] textInTwoLangs  = ConvertMongoToFdoTsStrings.GetSpanTexts(twoLangs) .ToArray();
			string[] textInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanTexts(twoStyles).ToArray();
			string[] textInTwoGuids  = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuids) .ToArray();
			string[] textInOneGuidOneStyle  = ConvertMongoToFdoTsStrings.GetSpanTexts(oneGuidOneStyle) .ToArray();
			string[] textInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsOneStyle).ToArray();
			string[] textInTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesNoLangs) .ToArray();
			string[] textInTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesOneLang) .ToArray();
			string[] textInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanTexts(twoGuidsTwoStylesTwoLangs).ToArray();

			Assert.That(textInZeroSpans.Length, Is.EqualTo(0));

			Assert.That(textInTwoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoLangs[0], Is.EqualTo("σπιθαμή"));
			Assert.That(textInTwoLangs[1], Is.EqualTo("portée"));

			Assert.That(textInTwoStyles.Length, Is.EqualTo(2));
			Assert.That(textInTwoStyles[0], Is.EqualTo("bold"));
			Assert.That(textInTwoStyles[1], Is.EqualTo("italic"));

			Assert.That(textInTwoGuids.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuids[0], Is.EqualTo("two"));
			Assert.That(textInTwoGuids[1], Is.EqualTo("guid"));

			Assert.That(textInOneGuidOneStyle.Length, Is.EqualTo(2));
			Assert.That(textInOneGuidOneStyle[0], Is.EqualTo("bold"));
			Assert.That(textInOneGuidOneStyle[1], Is.EqualTo("guid-containing"));

			Assert.That(textInTwoGuidsOneStyle.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsOneStyle[0], Is.EqualTo("two"));
			Assert.That(textInTwoGuidsOneStyle[1], Is.EqualTo("guid"));

			Assert.That(textInTwoGuidsTwoStylesNoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesNoLangs[0], Is.EqualTo("two (B)"));
			Assert.That(textInTwoGuidsTwoStylesNoLangs[1], Is.EqualTo("guid (I)"));

			Assert.That(textInTwoGuidsTwoStylesOneLang.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesOneLang[0], Is.EqualTo("two (B)"));
			Assert.That(textInTwoGuidsTwoStylesOneLang[1], Is.EqualTo("guid (I,fr)"));

			Assert.That(textInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));
			Assert.That(textInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("two (B,grc)"));
			Assert.That(textInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("guid (I,fr)"));
		}

		[Test]
		public void CanClassifySpansByLanguage()
		{
			// Spans will look like: <span lang="en" class="guid_123-456 styleName_DefaultText"</span>
			string[] langsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanLanguages(noSpans)  .ToArray();
			string[] langsInTwoLangs  = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoLangs) .ToArray();
			string[] langsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoStyles).ToArray();
			string[] langsInTwoGuids  = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuids) .ToArray();
			string[] langsInOneGuidOneStyle  = ConvertMongoToFdoTsStrings.GetSpanLanguages(oneGuidOneStyle) .ToArray();
			string[] langsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsOneStyle).ToArray();
			string[] langsInTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesNoLangs) .ToArray();
			string[] langsInTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesOneLang) .ToArray();
			string[] langsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanLanguages(twoGuidsTwoStylesTwoLangs).ToArray();

			Assert.That(langsInZeroSpans.Length, Is.EqualTo(0));

			Assert.That(langsInTwoLangs.Length,  Is.EqualTo(2));
			Assert.That(langsInTwoLangs[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoLangs[1], Is.EqualTo("fr"));

			Assert.That(langsInTwoStyles.Length, Is.EqualTo(0));
			Assert.That(langsInTwoGuids.Length,  Is.EqualTo(0));
			Assert.That(langsInOneGuidOneStyle.Length,  Is.EqualTo(0));
			Assert.That(langsInTwoGuidsOneStyle.Length, Is.EqualTo(0));

			Assert.That(langsInTwoGuidsTwoStylesNoLangs.Length,  Is.EqualTo(0));
			Assert.That(langsInTwoGuidsTwoStylesOneLang.Length,  Is.EqualTo(1));
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));

			Assert.That(langsInTwoGuidsTwoStylesOneLang[0],  Is.EqualTo("fr"));
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("fr"));
		}

		[Test]
		public void CanExtractGuidsFromSpans()
		{
			Guid[] guidsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanGuids(noSpans)  .ToArray();
			Guid[] guidsInTwoLangs  = ConvertMongoToFdoTsStrings.GetSpanGuids(twoLangs) .ToArray();
			Guid[] guidsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanGuids(twoStyles).ToArray();
			Guid[] guidsInTwoGuids  = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuids) .ToArray();
			Guid[] guidsInOneGuidOneStyle  = ConvertMongoToFdoTsStrings.GetSpanGuids(oneGuidOneStyle) .ToArray();
			Guid[] guidsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsOneStyle).ToArray();
			Guid[] guidsInTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesNoLangs) .ToArray();
			Guid[] guidsInTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesOneLang) .ToArray();
			Guid[] guidsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanGuids(twoGuidsTwoStylesTwoLangs).ToArray();

			Assert.That(guidsInZeroSpans.Length, Is.EqualTo(0));
			Assert.That(guidsInTwoLangs.Length,  Is.EqualTo(0));
			Assert.That(guidsInTwoStyles.Length, Is.EqualTo(0));
			Assert.That(guidsInTwoGuids.Length,  Is.EqualTo(2));
			Assert.That(guidsInOneGuidOneStyle.Length,  Is.EqualTo(1));
			Assert.That(guidsInTwoGuidsOneStyle.Length, Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesNoLangs.Length,  Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesOneLang.Length,  Is.EqualTo(2));
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));

			Assert.That(guidsInTwoGuids[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuids[1], Is.EqualTo(secondGuid));

			Assert.That(guidsInOneGuidOneStyle[0],  Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsOneStyle[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsOneStyle[1], Is.EqualTo(secondGuid));

			Assert.That(guidsInTwoGuidsTwoStylesNoLangs[0],  Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesNoLangs[1],  Is.EqualTo(secondGuid));
			Assert.That(guidsInTwoGuidsTwoStylesOneLang[0],  Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesOneLang[1],  Is.EqualTo(secondGuid));
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo(firstGuid));
			Assert.That(guidsInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo(secondGuid));
		}

		[Test]
		public void CanExtractStylesFromSpans()
		{
			string[] stylesInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanStyles(noSpans)  .ToArray();
			string[] stylesInTwoLangs  = ConvertMongoToFdoTsStrings.GetSpanStyles(twoLangs) .ToArray();
			string[] stylesInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanStyles(twoStyles).ToArray();
			string[] stylesInTwoGuids  = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuids) .ToArray();
			string[] stylesInOneGuidOneStyle  = ConvertMongoToFdoTsStrings.GetSpanStyles(oneGuidOneStyle) .ToArray();
			string[] stylesInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsOneStyle).ToArray();
			string[] stylesInTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesNoLangs) .ToArray();
			string[] stylesInTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesOneLang) .ToArray();
			string[] stylesInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanStyles(twoGuidsTwoStylesTwoLangs).ToArray();

			Assert.That(stylesInZeroSpans.Length, Is.EqualTo(0));
			Assert.That(stylesInTwoLangs.Length,  Is.EqualTo(0));
			Assert.That(stylesInTwoStyles.Length, Is.EqualTo(2));
			Assert.That(stylesInTwoGuids.Length,  Is.EqualTo(0));
			Assert.That(stylesInOneGuidOneStyle.Length,  Is.EqualTo(1));
			Assert.That(stylesInTwoGuidsOneStyle.Length, Is.EqualTo(1));
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs.Length,  Is.EqualTo(2));
			Assert.That(stylesInTwoGuidsTwoStylesOneLang.Length,  Is.EqualTo(2));
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(2));

			Assert.That(stylesInTwoStyles[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoStyles[1], Is.EqualTo("Italic"));
			Assert.That(stylesInOneGuidOneStyle[0],  Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsOneStyle[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs[0],  Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesNoLangs[1],  Is.EqualTo("Italic"));
			Assert.That(stylesInTwoGuidsTwoStylesOneLang[0],  Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesOneLang[1],  Is.EqualTo("Italic"));
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs[0], Is.EqualTo("Bold"));
			Assert.That(stylesInTwoGuidsTwoStylesTwoLangs[1], Is.EqualTo("Italic"));
		}

		[Test]
		public void CanExtractRunsFromSpans()
		{
			Run[] runsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanRuns(noSpans)  .ToArray();
			Run[] runsInTwoLangs  = ConvertMongoToFdoTsStrings.GetSpanRuns(twoLangs) .ToArray();
			Run[] runsInTwoStyles = ConvertMongoToFdoTsStrings.GetSpanRuns(twoStyles).ToArray();
			Run[] runsInTwoGuids  = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuids) .ToArray();
			Run[] runsInOneGuidOneStyle  = ConvertMongoToFdoTsStrings.GetSpanRuns(oneGuidOneStyle) .ToArray();
			Run[] runsInTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsOneStyle).ToArray();
			Run[] runsInTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesNoLangs) .ToArray();
			Run[] runsInTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesOneLang) .ToArray();
			Run[] runsInTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.GetSpanRuns(twoGuidsTwoStylesTwoLangs).ToArray();

			Assert.That(runsInZeroSpans.Length, Is.EqualTo(1));
			Assert.That(runsInTwoLangs.Length,  Is.EqualTo(5));
			Assert.That(runsInTwoStyles.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuids.Length,  Is.EqualTo(5));
			Assert.That(runsInOneGuidOneStyle.Length,  Is.EqualTo(5));
			Assert.That(runsInTwoGuidsOneStyle.Length, Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs.Length,  Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesOneLang.Length,  Is.EqualTo(5));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs.Length, Is.EqualTo(5));

			Assert.That(runsInZeroSpans[0].Content,   Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(runsInZeroSpans[0].Lang,      Is.Null);
			Assert.That(runsInZeroSpans[0].Guid,      Is.Null);
			Assert.That(runsInZeroSpans[0].StyleName, Is.Null);

			Assert.That(runsInTwoLangs[0].Content,   Is.EqualTo("foo"));
			Assert.That(runsInTwoLangs[0].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[0].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[0].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[1].Content,   Is.EqualTo("σπιθαμή"));
			Assert.That(runsInTwoLangs[1].Lang,      Is.EqualTo("grc"));
			Assert.That(runsInTwoLangs[1].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[1].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[2].Content,   Is.EqualTo("bar"));
			Assert.That(runsInTwoLangs[2].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[2].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[2].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[3].Content,   Is.EqualTo("portée"));
			Assert.That(runsInTwoLangs[3].Lang,      Is.EqualTo("fr"));
			Assert.That(runsInTwoLangs[3].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[3].StyleName, Is.Null);
			Assert.That(runsInTwoLangs[4].Content,   Is.EqualTo("baz"));
			Assert.That(runsInTwoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoLangs[4].StyleName, Is.Null);

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
			Assert.That(runsInTwoStyles[3].StyleName, Is.EqualTo("Italic"));
			Assert.That(runsInTwoStyles[4].Content,   Is.EqualTo(" text"));
			Assert.That(runsInTwoStyles[4].Lang,      Is.Null);
			Assert.That(runsInTwoStyles[4].Guid,      Is.Null);
			Assert.That(runsInTwoStyles[4].StyleName, Is.Null);

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
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[3].StyleName, Is.EqualTo("Italic"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Content,   Is.EqualTo(" classes, and two styles, but there are no language spans"));
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesNoLangs[4].StyleName, Is.Null);

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
			Assert.That(runsInTwoGuidsTwoStylesOneLang[3].StyleName, Is.EqualTo("Italic"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Content,   Is.EqualTo(" classes, and two styles, and one language span"));
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesOneLang[4].StyleName, Is.Null);

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
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[3].StyleName, Is.EqualTo("Italic"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Content,   Is.EqualTo(" classes, and two styles, and two language spans"));
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Lang,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].Guid,      Is.Null);
			Assert.That(runsInTwoGuidsTwoStylesTwoLangs[4].StyleName, Is.Null);
		}

		[Test]
		public void CanCreateTsStringsFromSpans()
		{
			ITsString tsStrFromZeroSpans = ConvertMongoToFdoTsStrings.SpanStrToTsString(noSpans,   _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoLangs  = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoLangs,  _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoStyles = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoStyles, _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoGuids  = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuids,  _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromOneGuidOneStyle  = ConvertMongoToFdoTsStrings.SpanStrToTsString(oneGuidOneStyle,  _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoGuidsOneStyle = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsOneStyle, _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoGuidsTwoStylesNoLangs  = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesNoLangs,  _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoGuidsTwoStylesOneLang  = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesOneLang,  _wsEn, _cache.WritingSystemFactory);
			ITsString tsStrFromTwoGuidsTwoStylesTwoLangs = ConvertMongoToFdoTsStrings.SpanStrToTsString(twoGuidsTwoStylesTwoLangs, _wsEn, _cache.WritingSystemFactory);

			int wsEn  = _wsEn;
			int wsFr  = _cache.WritingSystemFactory.GetWsFromStr("fr");
			int wsGrc = _cache.WritingSystemFactory.GetWsFromStr("grc");

			Assert.That(tsStrFromZeroSpans, Is.Not.Null);
			Assert.That(tsStrFromTwoLangs,  Is.Not.Null);
			Assert.That(tsStrFromTwoStyles, Is.Not.Null);
			Assert.That(tsStrFromTwoGuids,  Is.Not.Null);
			Assert.That(tsStrFromOneGuidOneStyle,  Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsOneStyle, Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs,  Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang,  Is.Not.Null);
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs, Is.Not.Null);

			Assert.That(tsStrFromZeroSpans.Text, Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(tsStrFromTwoLangs.Text,  Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(tsStrFromTwoStyles.Text, Is.EqualTo("this has bold and italic text"));
			Assert.That(tsStrFromTwoGuids.Text,  Is.EqualTo("this has two different guid classes, but no language spans"));
			Assert.That(tsStrFromOneGuidOneStyle.Text,  Is.EqualTo("this has bold and guid-containing text"));
			Assert.That(tsStrFromTwoGuidsOneStyle.Text, Is.EqualTo("this has two different guid classes, and the first is bold, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.Text,  Is.EqualTo("this has two (B) different guid (I) classes, and two styles, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.Text,  Is.EqualTo("this has two (B) different guid (I,fr) classes, and two styles, and one language span"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.Text, Is.EqualTo("this has two (B,grc) different guid (I,fr) classes, and two styles, and two language spans"));

			Assert.That(tsStrFromZeroSpans.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromZeroSpans.get_RunText(0), Is.EqualTo("fooσπιθαμήbarportéebaz"));
			Assert.That(tsStrFromTwoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoLangs.get_RunText(0), Is.EqualTo("foo"));
			Assert.That(tsStrFromTwoLangs.get_RunText(1), Is.EqualTo("σπιθαμή"));
			Assert.That(tsStrFromTwoLangs.get_RunText(2), Is.EqualTo("bar"));
			Assert.That(tsStrFromTwoLangs.get_RunText(3), Is.EqualTo("portée"));
			Assert.That(tsStrFromTwoLangs.get_RunText(4), Is.EqualTo("baz"));
			Assert.That(tsStrFromTwoStyles.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoStyles.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoStyles.get_RunText(1), Is.EqualTo("bold"));
			Assert.That(tsStrFromTwoStyles.get_RunText(2), Is.EqualTo(" and "));
			Assert.That(tsStrFromTwoStyles.get_RunText(3), Is.EqualTo("italic"));
			Assert.That(tsStrFromTwoStyles.get_RunText(4), Is.EqualTo(" text"));
			Assert.That(tsStrFromTwoGuids.RunCount, Is.EqualTo(1));
			Assert.That(tsStrFromTwoGuids.get_RunText(0), Is.EqualTo("this has two different guid classes, but no language spans"));
			Assert.That(tsStrFromOneGuidOneStyle.RunCount, Is.EqualTo(3));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(1), Is.EqualTo("bold"));
			Assert.That(tsStrFromOneGuidOneStyle.get_RunText(2), Is.EqualTo(" and guid-containing text"));
			Assert.That(tsStrFromTwoGuidsOneStyle.RunCount, Is.EqualTo(3));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(1), Is.EqualTo("two"));
			Assert.That(tsStrFromTwoGuidsOneStyle.get_RunText(2), Is.EqualTo(" different guid classes, and the first is bold, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(1), Is.EqualTo("two (B)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(3), Is.EqualTo("guid (I)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesNoLangs.get_RunText(4), Is.EqualTo(" classes, and two styles, but there are no language spans"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(1), Is.EqualTo("two (B)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(3), Is.EqualTo("guid (I,fr)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesOneLang.get_RunText(4), Is.EqualTo(" classes, and two styles, and one language span"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.RunCount, Is.EqualTo(5));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(0), Is.EqualTo("this has "));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(1), Is.EqualTo("two (B,grc)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(2), Is.EqualTo(" different "));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(3), Is.EqualTo("guid (I,fr)"));
			Assert.That(tsStrFromTwoGuidsTwoStylesTwoLangs.get_RunText(4), Is.EqualTo(" classes, and two styles, and two language spans"));

			Assert.That(GetStyle(tsStrFromZeroSpans, 0), Is.Null);
			Assert.That(GetWs(tsStrFromZeroSpans, 0), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 1), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 3), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoLangs, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoLangs, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoLangs, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoLangs, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoLangs, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoLangs, 4), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoStyles, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStyles, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoStyles, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoStyles, 3), Is.EqualTo("Italic"));
			Assert.That(GetStyle(tsStrFromTwoStyles, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoStyles, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 3), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoStyles, 4), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoGuids, 0), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuids, 0), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromOneGuidOneStyle, 2), Is.Null);
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromOneGuidOneStyle, 2), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsOneStyle, 2), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsOneStyle, 2), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 3), Is.EqualTo("Italic"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesNoLangs, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 3), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesNoLangs, 4), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 3), Is.EqualTo("Italic"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesOneLang, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 1), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesOneLang, 4), Is.EqualTo(wsEn));

			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 0), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 1), Is.EqualTo("Bold"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 2), Is.Null);
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 3), Is.EqualTo("Italic"));
			Assert.That(GetStyle(tsStrFromTwoGuidsTwoStylesTwoLangs, 4), Is.Null);
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 0), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 1), Is.EqualTo(wsGrc));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 2), Is.EqualTo(wsEn));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 3), Is.EqualTo(wsFr));
			Assert.That(GetWs(tsStrFromTwoGuidsTwoStylesTwoLangs, 4), Is.EqualTo(wsEn));
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
