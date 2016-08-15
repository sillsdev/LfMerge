// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.DataConverters;
using NUnit.Framework;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests.Fdo.DataConverters
{
	public class ConvertMongoToFdoTsStringsTests // : FdoTestBase
	{
		// *****************
		//     Test data
		// *****************
		private string twoLangs  = "foo<span lang=\"grc\">σπιθαμή</span>bar<span lang=\"fr\">portée</span>baz";
		private string noSpans   = "fooσπιθαμήbarportéebaz";
		private string twoStyles = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"styleName_Italic\">italic</span> text";
		private string twoGuids  = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, but no language spans";
		private string oneGuidOneStyle  = "this has <span class=\"styleName_Bold\">bold</span> and <span class=\"guid_01234567-1234-4321-89ab-0123456789ab\">guid-containing</span> text";
		private string twoGuidsOneStyle = "this has <span class=\"guid_01234567-1234-4321-89ab-0123456789ab styleName_Bold\">two</span> different <span class=\"guid_98765432-1234-4321-89ab-0123456789ab\">guid</span> classes, and the first is bold, but there are no language spans";

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

			Assert.That(langsInZeroSpans.Length, Is.EqualTo(0));

			Assert.That(langsInTwoLangs.Length,  Is.EqualTo(2));
			Assert.That(langsInTwoLangs[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoLangs[1], Is.EqualTo("fr"));

			Assert.That(langsInTwoStyles.Length, Is.EqualTo(0));
			Assert.That(langsInTwoGuids.Length,  Is.EqualTo(0));
			Assert.That(langsInOneGuidOneStyle.Length,  Is.EqualTo(0));
			Assert.That(langsInTwoGuidsOneStyle.Length, Is.EqualTo(0));
		}
	}
}

