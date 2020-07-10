// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Net.NetworkInformation;
using NUnit.Framework;

namespace LfMerge.Core.Tests
{
	[TestFixture]
	public class LanguageDepotProjectTests
	{
		private TestEnvironment _env;

		[OneTimeSetUp]
		public void FixtureSetup()
		{
			_env = new TestEnvironment();

			var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			var uri = new Uri("mongodb://" + _env.Settings.MongoDbHostNameAndPort);
			if (ipGlobalProperties.GetActiveTcpListeners().Count(t => t.Port == uri.Port) == 0)
			{
				Assert.Ignore("Ignoring tests because MongoDB doesn't seem to be running on {0}.",
					_env.Settings.MongoDbHostNameAndPort);
			}
		}

		[OneTimeTearDown]
		public void FixtureTearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void ExistingProject()
		{
			// relies on a project being manually added to MongoDB with projectCode "proja"
			// To make this stop being ignored, run the following command in the "scriptureforge" database of your local MongoDB:
			// db.projects.insert({projectCode: "proja", projectName: "ZZZ Project A for unit tests", sendReceiveProjectIdentifier: "proja-langdepot", sendReceiveProject: {name: "Fake project for unit tests", repository: "http://public.example.com", role: "manager"} })

			// Setup
			var sut = new LanguageDepotProject(_env.Settings, _env.Logger);

			// Exercise
			try
			{
				sut.Initialize("proja");
			}
			catch (ArgumentException e)
			{
				if (e.Message.StartsWith("Can't find project code"))
					Assert.Ignore("Can't run this test until a project named \"proja\" exists in local MongoDB");
				else
					throw;
			}

			// Verify
			Assert.That(sut.Identifier, Is.EqualTo("proja-langdepot"));
			Assert.That(sut.Repository, Contains.Substring("public"));
		}

		[Test]
		public void NonexistingProject()
		{
			// Setup
			var sut = new LanguageDepotProject(_env.Settings, _env.Logger);

			// Exercise/Verify
			Assert.That(() => sut.Initialize("nonexisting"), Throws.ArgumentException);
		}
	}
}

