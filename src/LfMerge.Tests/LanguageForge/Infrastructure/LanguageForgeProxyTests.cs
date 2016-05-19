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

namespace LfMerge.Tests.LanguageForge.Infrastructure
{
	[TestFixture]
	[Category("IntegrationTests")]
	public class LanguageForgeProxyTests
	{
		[Test]
		public void ListUsers_CanCallPhpClass()
		{
			// Setup
			var sut = new LanguageForgeProxy();

			// Exercise
			string output = sut.ListUsers();

			// Verify
			var result = JsonConvert.DeserializeObject<Dictionary<string, Object>>(output);
			Assert.That(output, Is.Not.Empty);
			Assert.That(result["count"], Is.GreaterThan(0));
		}

		[Test]
		[Explicit("Assumes PHP unit tests have been run once")]
		public void UpdateCustomFieldViews_ReturnsProjectId()
		{
			// Setup
			var customFieldSpecs = new List<CustomFieldSpec>();
			customFieldSpecs.Add(new CustomFieldSpec("customField_entry_testMultiPara", "OwningAtom"));
			customFieldSpecs.Add(new CustomFieldSpec("customField_examples_testOptionList", "ReferenceAtom"));
			var sut = new LanguageForgeProxy();

			// Exercise
			string output = sut.UpdateCustomFieldViews("TestCode1", customFieldSpecs, true);

			// Verify
			var result = JsonConvert.DeserializeObject<string>(output);
			Assert.That(output, Is.Not.Empty);
			Assert.That(output, Is.Not.EqualTo("false"));
			Assert.That(result, Is.Not.Empty);
		}
	}
}

