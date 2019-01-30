// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using NUnit.Framework;
using LfMerge.Core.Actions.Infrastructure;
using SIL.LCModel;

namespace LfMerge.Core.Tests.Actions.Infrastructure
{
	[TestFixture]
	public class ChorusHelperTests
	{
		private TestEnvironment _env;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment(registerSettingsModelDouble: false);
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				LanguageForgeProjectAccessor.Reset(); // This disposes of lfProj
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
		public void GetSyncUri_SimpleName()
		{
			var proj = LanguageForgeProject.Create("test");
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			Assert.That(chorusHelper.GetSyncUri(proj),
				Is.EqualTo("http://x:x@hg-public.languagedepot.org/test"));
		}

		[Test]
		public void GetSyncUri_NameWithSpace()
		{
			var proj = LanguageForgeProject.Create("test project");
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			Assert.That(chorusHelper.GetSyncUri(proj),
				Is.EqualTo("http://x:x@hg-public.languagedepot.org/test+project"));
		}

		[Test]
		public void SetModelVersion()
		{
			ChorusHelper.SetModelVersion(123456);
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			Assert.That(chorusHelper.ModelVersion, Is.EqualTo(123456));
		}

		[Test]
		public void RemoteDataIsForDifferentModelVersion_SameVersion()
		{
			ChorusHelper.SetModelVersion(LcmCache.ModelVersion);
			Assert.That(ChorusHelper.RemoteDataIsForDifferentModelVersion, Is.False);
		}

		[Test]
		public void RemoteDataIsForDifferentModelVersion_DifferentVersion()
		{
			ChorusHelper.SetModelVersion(7000060);
			Assert.That(ChorusHelper.RemoteDataIsForDifferentModelVersion, Is.True);
		}
	}
}

