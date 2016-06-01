// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Net.NetworkInformation;
using NUnit.Framework;

namespace LfMerge.Tests
{
	[TestFixture]
	public class LanguageDepotProjectTests
	{
		private TestEnvironment _env;

		[TestFixtureSetUp]
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

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			_env.Dispose();
		}

		[Test]
		[Ignore("Currently we don't have the required fields in mongodb")]
		public void ExistingProject()
		{
			// relies on a project being manually added to MongoDB with projectCode "proja"

			// Setup
			var sut = new LanguageDepotProject(_env.Settings);

			// Exercise
			sut.Initialize("proja");

			// Verify
			Assert.That(sut.Identifier, Is.EqualTo("proja-langdepot"));
			Assert.That(sut.Repository, Contains.Substring("public"));
		}

		[Test]
		public void NonexistingProject()
		{
			// Setup
			var sut = new LanguageDepotProject(_env.Settings);

			// Exercise/Verify
			Assert.That(() => sut.Initialize("nonexisting"), Throws.ArgumentException);
		}
	}
}

