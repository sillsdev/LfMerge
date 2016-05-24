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
		public void GetCustomFieldsForThisCmObject_ShouldGetCustomFieldSettings()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, TestProjectCode);
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			Dictionary<string, LfConfigFieldBase> lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			ConvertFdoToMongoCustomField converter = new ConvertFdoToMongoCustomField(cache, new Logging.SyslogLogger());

			// Exercise
			BsonDocument customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", lfCustomFieldList);

			// Verify
			List<string> expectedCustomFieldNames = new List<string>
			{
				"customField_entry_Cust_Date",
				"customField_entry_Cust_MultiPara",
				"customField_entry_Cust_Number",
				"customField_entry_Cust_Single_Line",
				"customField_entry_Cust_Single_ListRef"
			};
			CollectionAssert.AreEqual(customDataDocument[0].AsBsonDocument.Names, expectedCustomFieldNames);
		}

	}
}

