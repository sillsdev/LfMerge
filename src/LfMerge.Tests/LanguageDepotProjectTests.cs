// Copyright (c) 2015 SIL International
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
			var uri = new Uri("mongodb://" + LfMergeSettings.Current.MongoDbHostNameAndPort);
			if (ipGlobalProperties.GetActiveTcpListeners().Count(t => t.Port == uri.Port) == 0)
			{
				Assert.Ignore("Ignoring tests because MongoDB doesn't seem to be running on {0}.",
					LfMergeSettings.Current.MongoDbHostNameAndPort);
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
			// Exercise
			var sut = new LanguageDepotProject("proja");

			// Verify
			Assert.That(sut.Username, Is.EqualTo("proja-user"));
			Assert.That(sut.Password, Is.EqualTo("proja-pw"));
			Assert.That(sut.ProjectCode, Is.EqualTo("proja-langdepot"));
		}

		[Test]
		public void NonexistingProject()
		{
			Assert.That(() => new LanguageDepotProject("nonexisting"), Throws.ArgumentException);
		}
	}
}

