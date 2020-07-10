// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

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

		public override void BeforeTest(ITest testDetails)
		{
			_env = new TestEnvironment();
		}

		public override void AfterTest(ITest testDetails)
		{
			_env.Dispose();
		}
	}
}

