// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.Core.LanguageForge.Infrastructure;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests.LanguageForge.Infrastructure
{
	[TestFixture]
	[Category("IntegrationTests")]
	public class LanguageForgeProxyTests
	{
		private const string testProjectCode = "testlangproj";
		private const int originalNumOfLcmEntries = 63;
		private TemporaryFolder LanguageForgeFolder;
		private TestEnvironment _env;

		[OneTimeSetUp]
		public void FixtureSetUp()
		{
			LanguageForgeFolder = new TemporaryFolder("LcmTestFixture");
			_env = new TestEnvironment(
				resetLfProjectsDuringCleanup: false,
				languageForgeServerFolder: LanguageForgeFolder,
				registerLfProxyMock: false
			);
		}

		[OneTimeTearDown]
		public void FixtureTearDown()
		{
			try
			{
				LanguageForgeProjectAccessor.Reset(); // This disposes of lfProj
				LanguageForgeFolder.Dispose();
				_env.Dispose();
			}
			catch (Exception)
			{
				// This can happen if the objects already got disposed somewhere else.
				// It doesn't really matter since we're in the process of doing cleanup anyways.
				// So just ignore the exception.
			}
		}

		[Test]
		[Explicit("Requires users in the mongo database, i.e. requires setup languageforge database")]
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

