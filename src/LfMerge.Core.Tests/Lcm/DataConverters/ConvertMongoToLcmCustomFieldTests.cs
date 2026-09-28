// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
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
	}
}
