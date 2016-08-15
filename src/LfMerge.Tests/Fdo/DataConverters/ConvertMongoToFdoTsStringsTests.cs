// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Text.RegularExpressions;
using LfMerge.DataConverters;
using NUnit.Framework;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests.Fdo.DataConverters
{
	public class ConvertMongoToFdoTsStringsTests // : FdoTestBase
	{
		private string hasSpans = "foo<span lang=\"grc\">σπιθαμή</span>bar<span lang=\"fr\">portée</span>baz";
		private string noSpans = "fooσπιθαμήbarportéebaz";

		public ConvertMongoToFdoTsStringsTests()
		{
		}

		[Test]
		public void CanDetectSpans()
		{
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(noSpans),  Is.EqualTo(0));
			Assert.That(ConvertMongoToFdoTsStrings.SpanCount(hasSpans), Is.EqualTo(2));
		}

		[Test]
		public void CanExtractTextInsideSpans()
		{
			string[] textInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanTexts(noSpans);
			string[] textInTwoSpans  = ConvertMongoToFdoTsStrings.GetSpanTexts(hasSpans);

			Assert.That(textInZeroSpans.Length, Is.EqualTo(0));
			Assert.That(textInTwoSpans.Length,  Is.EqualTo(2));
			Assert.That(textInTwoSpans[0], Is.EqualTo("σπιθαμή"));
			Assert.That(textInTwoSpans[1], Is.EqualTo("portée"));
		}

		[Test]
		public void CanClassifySpansByLanguage()
		{
			// Spans will look like: <span lang="en" class="guid_123-456 styleName_DefaultText"</span>
			string[] langsInZeroSpans = ConvertMongoToFdoTsStrings.GetSpanLanguages(noSpans);
			string[] langsInTwoSpans  = ConvertMongoToFdoTsStrings.GetSpanLanguages(hasSpans);

			Assert.That(langsInZeroSpans.Length, Is.EqualTo(0));
			Assert.That(langsInTwoSpans.Length,  Is.EqualTo(2));
			Assert.That(langsInTwoSpans[0], Is.EqualTo("grc"));
			Assert.That(langsInTwoSpans[1], Is.EqualTo("fr"));
		}
	}
}

