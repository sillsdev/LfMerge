// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Infrastructure;
using LfMerge.Tests.Fdo;
using MongoDB.Bson;
using Newtonsoft.Json;
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
			ConvertFdoToMongoCustomField converter = new ConvertFdoToMongoCustomField(cache, new Logging.SyslogLogger());
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
			ConvertFdoToMongoCustomField converter = new ConvertFdoToMongoCustomField(cache, new Logging.SyslogLogger());
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
			ConvertFdoToMongoCustomField converter = new ConvertFdoToMongoCustomField(cache, new Logging.SyslogLogger());
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);

			// Exercise
			Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			BsonDocument customDataDocument = converter.GetCustomFieldsForThisCmObject(entry, "entry", _listConverters, _lfCustomFieldList);

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

		[Test]
		public void RunClass_listUsers_CanCallPhpClass()
		{
			// Setup
			string className = "Api\\Model\\Command\\UserCommands";
			string methodName = "listUsers";
			var parameters = new List<Object>();

			// Exercise
			string output = PhpConnection.RunClass(className, methodName, parameters);

			// Verify
			var result = JsonConvert.DeserializeObject<Dictionary<string, Object>>(output);
			Assert.That(output, Is.Not.Empty);
			Assert.That(result["count"], Is.GreaterThan(0));
		}

		[Test, Explicit("Assumes PHP unit tests have been run once")]
		public void RunClass_updateCustomFieldViews_ReturnsProjectId()
		{
			// Setup
			string projectCode = "TestCode1";
			string className = "Api\\Model\\Languageforge\\Lexicon\\Command\\LexProjectCommands";
			string methodName = "updateCustomFieldViews";
			var customFieldSpecs = new List<CustomFieldSpec>();
			customFieldSpecs.Add(new CustomFieldSpec("customField_entry_testMultiPara", "OwningAtom"));
			customFieldSpecs.Add(new CustomFieldSpec("customField_examples_testOptionList", "ReferenceAtom"));
			var parameters = new List<Object>();
			parameters.Add(projectCode);
			parameters.Add(customFieldSpecs);

			// Exercise
			string output = PhpConnection.RunClass(className, methodName, parameters, true);

			// Verify
			var result = JsonConvert.DeserializeObject<string>(output);
			Assert.That(output, Is.Not.Empty);
			Assert.That(output, Is.Not.EqualTo("false"));
			Assert.That(result, Is.Not.Empty);
		}
	}
}

