// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Config;
using LfMerge.Tests.Fdo;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.FieldWorks.FDO;

namespace LfMerge.Tests.Fdo.DataConverters
{
	public class ConvertFdoToMongoCustomFieldTests : FdoTestBase
	{
		[Test]
		public void GetCustomFieldForThisCmObject_ShouldGetSingleLineAll()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, TestProjectCode);
			var cache = lfProject.FieldWorksProject.Cache;
			var converter = new ConvertFdoToMongoCustomField(cache,
				new TestLogger(TestContext.CurrentContext.Test.Name));
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Exercise
			Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			BsonDocument customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters, _lfCustomFieldList);

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
			var lfProject = LanguageForgeProject.Create(_env.Settings, TestProjectCode);
			var cache = lfProject.FieldWorksProject.Cache;
			var converter = new ConvertFdoToMongoCustomField(cache,
				new TestLogger(TestContext.CurrentContext.Test.Name));
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Exercise
			Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			ILexSense[] senses = entry.SensesOS.ToArray();
			BsonDocument customDataDocument = converter.GetCustomFieldsForThisCmObject(senses[0], "senses", _listConverters, _lfCustomFieldList);

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
			var lfProject = LanguageForgeProject.Create(_env.Settings, TestProjectCode);
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			Dictionary<string, LfConfigFieldBase> lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			ConvertFdoToMongoCustomField converter = new ConvertFdoToMongoCustomField(cache,
				new TestLogger(TestContext.CurrentContext.Test.Name));

			// Exercise
			Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			BsonDocument customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters, lfCustomFieldList);

			// Verify
			List<string> expectedCustomFieldNames = new List<string>
			{
				"customField_entry_Cust_Date",
				"customField_entry_Cust_MultiPara",
				"customField_entry_Cust_Number",
				"customField_entry_Cust_Single_Line",
				"customField_entry_Cust_Single_Line_All",
				"customField_entry_Cust_Single_ListRef"
			};
			CollectionAssert.AreEqual(customDataDocument[0].AsBsonDocument.Names, expectedCustomFieldNames);
		}

	}
}

