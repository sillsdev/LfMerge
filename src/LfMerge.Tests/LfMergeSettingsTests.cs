// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using NUnit.Framework;
using SIL.TestUtilities;

namespace LfMerge.Tests
{
	[TestFixture]
	public class LfMergeSettingsTests
	{
		private TemporaryFolder _tempDir;

		[SetUp]
		public void Setup()
		{
			_tempDir = new TemporaryFolder(Path.GetRandomFileName());
			LfMergeSettings.ConfigDir = _tempDir.Path;
		}

		[TearDown]
		public void TearDown()
		{
			_tempDir.Dispose();
			LfMergeSettingsAccessor.ResetCurrent();
		}

		[Test]
		public void SaveLoadSettings_Roundtrip()
		{
			// Setup
			LfMergeSettings.Initialize(_tempDir.Path, "Dir1", "Dir2");
			var expected = LfMergeSettings.Current;

			// Exercise
			LfMergeSettings.Current.SaveSettings();
			var sut = LfMergeSettings.LoadSettings();

			// Verify
			Assert.That(sut, Is.EqualTo(expected));
			Assert.That(sut, Is.SameAs(LfMergeSettings.Current));
			Assert.That(sut, Is.Not.SameAs(expected));
		}

		[Test]
		public void LoadSettings_NoFileReturnsDefault()
		{
			// Setup
			LfMergeSettings.Initialize(_tempDir.Path, "Dir1", "Dir2");
			var previous = LfMergeSettings.Current;

			// Exercise
			var settings = LfMergeSettings.LoadSettings();

			// Verify
			Assert.That(settings, Is.Not.EqualTo(previous));
			Assert.That(settings, Is.SameAs(LfMergeSettings.Current));
		}

		[Test]
		public void LoadSettings_FromFile()
		{
			// Setup
			const string json = "{\"QueueDirectories\":[null,\"/root/foo/boo/mergequeue\"," +
				"\"/root/foo/boo/sendqueue\",\"/root/foo/boo/receivequeue\"," +
				"\"/root/foo/boo/commitqueue\"],\"ProjectsDirectory\":\"/root/foo/boo/Dir3\"," +
				"\"DefaultProjectsDirectory\":\"/root/foo/boo/Dir3\"," +
				"\"TemplateDirectory\":\"/root/foo/boo/Dir99\"," +
				"\"StateDirectory\":\"/root/foo/boo/state\"," +
				"\"ConfigDir\":\"/root/foo/boo\"," +
				"\"WebWorkDirectory\":\"/root/foo/boo/Dir3\"," +
				"\"MongoDbHostNameAndPort\":\"www.example.com:7429\"}";

			LfMergeSettings.Initialize();
			Directory.CreateDirectory(LfMergeSettings.ConfigDir);
			File.WriteAllText(LfMergeSettings.ConfigFile, json);

			// Exercise
			var sut = LfMergeSettings.LoadSettings();

			// Verify
			Assert.That(sut.ProjectsDirectory, Is.EqualTo("/root/foo/boo/Dir3"));
			Assert.That(sut.DefaultProjectsDirectory, Is.EqualTo("/root/foo/boo/Dir3"));
			Assert.That(sut.WebWorkDirectory, Is.EqualTo("/root/foo/boo/Dir3"));
			Assert.That(sut.TemplateDirectory, Is.EqualTo("/root/foo/boo/Dir99"));
			Assert.That(sut.StateDirectory, Is.EqualTo("/root/foo/boo/state"));
			Assert.That(LfMergeSettings.ConfigFile, Is.EqualTo("/root/foo/boo/sendreceive.conf"));
			Assert.That(sut.MongoDbHostNameAndPort, Is.EqualTo("www.example.com:7429"));
		}

	}
}

