// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;

namespace LfMerge.Core.Tests
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
	public class TestEnvironmentAttribute: TestActionAttribute
	{
		private TestEnvironment _env;

		public override ActionTargets Targets
		{
			get
			{
				return ActionTargets.Test;
			}
		}

		public override void BeforeTest(TestDetails testDetails)
		{
			_env = new TestEnvironment();
		}

		public override void AfterTest(TestDetails testDetails)
		{
			_env.Dispose();
		}
	}
}

