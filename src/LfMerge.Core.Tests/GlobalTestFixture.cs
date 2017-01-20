// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using NUnit.Framework;

namespace LfMerge.Core.Tests
{
	[SetUpFixture]
	public class GlobalTestFixture
	{
		// Setup and cleanup code that should run ONCE, before/after ALL unit tests

		[SetUp]
		public void InitializeAllTests()
		{
		}

		[TearDown]
		public void CleanupAllTests()
		{
		}
	}
}

