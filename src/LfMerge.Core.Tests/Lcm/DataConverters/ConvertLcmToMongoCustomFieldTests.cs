// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using LfMerge.Core.DataConverters;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.LCModel;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	public class ConvertLcmToMongoCustomFieldTests : LcmTestBase
	{
		[Test]
		public void GetCustomFieldForThisCmObject_ShouldGetSingleLineAll()
		{
			// Setup
			var lfProject = _lfProj;
			var converter = new ConvertLcmToMongoCustomField(_cache, _servLoc,
				new TestLogger(TestContext.CurrentContext.Test.Name));
			var entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = _servLoc.GetInstance<ILexEntryRepository>().GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Exercise
			var customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters);

			// Verify English and french values
			Assert.That(customDataDocument[0]["customField_entry_Cust_Single_Line_All"].AsBsonDocument.GetElement(0).Value["value"].ToString(),
				Is.EqualTo("Some custom text"));
			Assert.That(customDataDocument[0]["customField_entry_Cust_Single_Line_All"].AsBsonDocument.GetElement(1).Value["value"].ToString(),
				Is.EqualTo("French custom text"));
		}

		[Test]
		public void GetCustomFieldForThisCmObject_ShouldGetMultiListRef()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = _cache;
			var converter = new ConvertLcmToMongoCustomField(cache, _servLoc,
				new TestLogger(TestContext.CurrentContext.Test.Name));
			var entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = _servLoc.GetInstance<ILexEntryRepository>().GetObject(entryGuid);
			Assert.That(entry, Is.Not.Null);

			// Exercise
			var senses = entry.SensesOS.ToArray();
			var customDataDocument = converter.GetCustomFieldsForThisCmObject(senses[0], "senses", _listConverters);

			// Verify.  (Note, in the fwdata file, the custom item labels are in reverse order)
			Assert.That(customDataDocument[0].AsBsonDocument["customField_senses_Cust_Multi_ListRef"]["values"][1].ToString(),
				Is.EqualTo("fci"));
			Assert.That(customDataDocument[0].AsBsonDocument["customField_senses_Cust_Multi_ListRef"]["values"][0].ToString(),
				Is.EqualTo("sci"));
		}

		[Test]
		public void GetCustomFieldsForThisCmObject_ShouldGetCustomFieldSettings()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = _cache;
			var entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = _servLoc.GetInstance<ILexEntryRepository>().GetObject(entryGuid);
			Assert.That(entry, Is.Not.Null);
			var converter = new ConvertLcmToMongoCustomField(cache, _servLoc,
				new TestLogger(TestContext.CurrentContext.Test.Name));

			// Exercise
			var customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters);

			// Verify
			var expectedCustomFieldNames = new List<string>
			{
				"customField_entry_Cust_Date",
				"customField_entry_Cust_MultiPara",
				"customField_entry_Cust_Number",
				"customField_entry_Cust_Single_Line",
				"customField_entry_Cust_Single_Line_All",
				"customField_entry_Cust_Single_ListRef"
			};
			CollectionAssert.AreEquivalent(expectedCustomFieldNames, customDataDocument[0].AsBsonDocument.Names);
		}

	}
}

