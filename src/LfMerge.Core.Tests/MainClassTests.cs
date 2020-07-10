// Copyright (c) 2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Net.NetworkInformation;
using LfMerge.Core.Actions;
using LfMerge.Core.Tests;
using NUnit.Framework;

namespace LfMerge.Core
{
	[TestFixture]
	public class MainClassTests
	{
		private TestEnvironment _env;

		[OneTimeSetUp]
		public void FixtureSetup()
		{
			_env = new TestEnvironment(registerProcessingStateDouble: false);
		}

		[OneTimeTearDown]
		public void FixtureTearDown()
		{
			_env.Dispose();
		}

		[TestCase("7000060", ExpectedResult = false)]
		[TestCase("7000067", ExpectedResult = false)]
		[TestCase("7000068", ExpectedResult = true)]
		[TestCase("7000069", ExpectedResult = true)]
		[TestCase("7000071", ExpectedResult = false)]
		[TestCase("7000072", ExpectedResult = true)]
		public bool IsSupportedModelVersion(string modelVersion)
		{
			return MainClass.IsSupportedModelVersion(modelVersion);
		}

		[Test]
		public void IsSupportedModelVersion_InvalidVersion_Throws()
		{
			Assert.That(() => MainClass.IsSupportedModelVersion("foo"), Throws.ArgumentException);
		}

		[Test]
		public void StartLfMerge_UnsupportedVersion_SetsErrorCode()
		{
			Assert.That(MainClass.StartLfMerge("someproject", ActionNames.None, "7000071", true),
				Is.EqualTo(2));

			var state = ProcessingState.Deserialize("someproject");
			Assert.That(state.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(state.ErrorCode, Is.EqualTo((int)ProcessingState.ErrorCodes.UnsupportedModelVersion));
			Assert.That(state.ErrorMessage, Is.EqualTo("Project 'someproject' has unsupported model version '7000071'."));
		}
	}
}
