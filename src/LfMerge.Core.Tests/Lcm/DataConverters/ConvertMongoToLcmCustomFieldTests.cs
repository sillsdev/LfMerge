// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Linq;
using LfMerge.Core.DataConverters;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.LCModel;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// Writing custom fields from LF into LCM.
	///
	/// Cust_Single_Line_All in the test project is a MultiUnicode field with English and French
	/// alternatives. That is the shape every text custom field an LF project owns takes: LF has
	/// never offered a single-string custom field, so a field created there is always multilingual
	/// even when only one writing system is filled in.
	/// </summary>
	public class ConvertMongoToLcmCustomFieldTests : LcmTestBase
	{
		private const string MultiTextField = "customField_entry_Cust_Single_Line_All";
		// A String field, whose writing system is the magic "first analysis" selector.
		private const string SingleStringField = "customField_entry_Cust_Single_Line";

		private static BsonDocument MultiText(params string[] wsThenValue)
		{
			var multiText = new BsonDocument();
			for (int i = 0; i < wsThenValue.Length; i += 2)
				multiText.Add(wsThenValue[i], new BsonDocument("value", wsThenValue[i + 1]));
			return multiText;
		}

		private ILexEntry TestEntry()
		{
			var entry = _servLoc.GetInstance<ILexEntryRepository>().GetObject(Guid.Parse(TestEntryGuidStr));
			Assert.That(entry, Is.Not.Null);
			return entry;
		}

		private ConvertMongoToLcmCustomField Converter()
		{
			return new ConvertMongoToLcmCustomField(_cache, _servLoc,
				new TestLogger(TestContext.CurrentContext.Test.Name), _wsEn);
		}

		/// <summary>Reads the entry's custom fields back out through the converter going the other way.</summary>
		private BsonDocument ReadBack(ILexEntry entry)
		{
			var reader = new ConvertLcmToMongoCustomField(_cache, _servLoc,
				new TestLogger(TestContext.CurrentContext.Test.Name));
			return reader.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters)[0].AsBsonDocument;
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldWriteEveryMultiUnicodeAlternative()
		{
			// Setup
			ILexEntry entry = TestEntry();
			var customFields = new BsonDocument(MultiTextField,
				MultiText("en", "Updated English", "fr", "Updated French"));

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry", customFields, null);

			// Verify
			BsonDocument written = ReadBack(entry)[MultiTextField].AsBsonDocument;
			Assert.That(written["en"]["value"].AsString, Is.EqualTo("Updated English"));
			Assert.That(written["fr"]["value"].AsString, Is.EqualTo("Updated French"));
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldWriteAFieldThatWasEmptyInLcm()
		{
			// The migration case: LF holds the only copy, and LCM has nothing in the field yet.
			// Emptying it leaves the alternatives in place holding empty strings rather than removing
			// them, so the precondition checks the contents rather than the presence of the key.
			ILexEntry entry = TestEntry();
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(MultiTextField, MultiText("en", "", "fr", "")), null);
			foreach (BsonElement alternative in ReadBack(entry)[MultiTextField].AsBsonDocument)
				Assert.That(alternative.Value["value"].AsString, Is.Empty,
					$"precondition: {alternative.Name} should have been emptied");

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(MultiTextField, MultiText("en", "Brand new")), null);

			// Verify
			Assert.That(ReadBack(entry)[MultiTextField]["en"]["value"].AsString, Is.EqualTo("Brand new"));
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldClearAnAlternativeLfNoLongerHas()
		{
			// The reverse converter exports every alternative LCM holds, so one missing from LF is
			// one the user deleted there -- not one LF never knew about.
			ILexEntry entry = TestEntry();
			Assert.That(ReadBack(entry)[MultiTextField].AsBsonDocument.Contains("fr"), Is.True,
				"precondition: the test project's field starts with a French alternative");

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(MultiTextField, MultiText("en", "English only")), null);

			// Verify
			BsonDocument written = ReadBack(entry)[MultiTextField].AsBsonDocument;
			Assert.That(written["en"]["value"].AsString, Is.EqualTo("English only"));
			Assert.That(written.Contains("fr"), Is.False, "French should have been cleared, not left stale");
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldSkipUnidentifiedWritingSystems()
		{
			// Setup
			ILexEntry entry = TestEntry();

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(MultiTextField,
					MultiText("en", "Kept", "zzz-x-nonesuch", "Dropped")), null);

			// Verify
			BsonDocument written = ReadBack(entry)[MultiTextField].AsBsonDocument;
			Assert.That(written["en"]["value"].AsString, Is.EqualTo("Kept"));
			Assert.That(written.Contains("zzz-x-nonesuch"), Is.False);
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldResolveANonCanonicalTagToTheCanonicalWritingSystem()
		{
			// LF keeps a tag as typed, while LCM knows the writing system only by its canonical id:
			// "fr-Latn" canonicalizes to "fr", French's suppress-script being dropped. Looked up
			// literally the key resolves to 0, and French would then be cleared as missing from LF.
			ILexEntry entry = TestEntry();

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(MultiTextField, MultiText("en", "English", "fr-Latn", "Français")), null);

			// Verify
			BsonDocument written = ReadBack(entry)[MultiTextField].AsBsonDocument;
			Assert.That(written.Contains("fr"), Is.True, "French should have been written, not cleared");
			Assert.That(written["fr"]["value"].AsString, Is.EqualTo("Français"));
		}

		[Test]
		public void SetCustomFieldsForThisCmObject_ShouldResolveANonCanonicalTagInAStringField()
		{
			// The String case resolved the key it picked with a literal GetWsFromStr too, and gave
			// up on the whole field when that returned 0, so the LF edit never reached LCM.
			ILexEntry entry = TestEntry();

			// Exercise
			Converter().SetCustomFieldsForThisCmObject(entry, "entry",
				new BsonDocument(SingleStringField, MultiText("fr-Latn", "Texte")), null);

			// Verify
			BsonDocument written = ReadBack(entry)[SingleStringField].AsBsonDocument;
			Assert.That(written["fr"]["value"].AsString, Is.EqualTo("Texte"));
		}

		[Test]
		public void SetCustomFieldData_FieldOfATypeItCannotWrite_IsReportedOncePerSyncNotOncePerObject()
		{
			// Setup: an owning sequence, a type the LF to LCM direction does not implement, on
			// three entries. The test project has no custom field of that type, so a built-in
			// field stands in for one; the writer does not tell them apart.
			var logger = new TestLogger(TestContext.CurrentContext.Test.Name);
			var converter = new ConvertMongoToLcmCustomField(_cache, _servLoc, logger, _wsEn);
			var entries = _servLoc.GetInstance<ILexEntryRepository>().AllInstances().Take(3).ToList();
			Assert.That(entries, Has.Count.EqualTo(3));

			// Exercise
			foreach (ILexEntry entry in entries)
				Assert.That(converter.SetCustomFieldData(entry.Hvo, LexEntryTags.kflidSenses, new BsonString("LF data"), null), Is.False);

			// Verify
			int reports = logger.Messages.Split('\n').Count(line => line.Contains("not written to LCM"));
			Assert.That(reports, Is.EqualTo(1));
		}
	}
}
