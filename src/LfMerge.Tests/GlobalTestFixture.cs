// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using SIL.WritingSystems;

namespace LfMerge.Tests
{
	[SetUpFixture]
	public class GlobalTestFixture
	{
		// Setup and cleanup code that should run ONCE, before/after ALL unit tests

		[SetUp]
		public void InitializeAllTests()
		{
			Sldr.Initialize();
		}

		[TearDown]
		public void CleanupAllTests()
		{
			Sldr.Cleanup();
		}
	}
}

