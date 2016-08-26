// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Core.LanguageForge.Infrastructure
{
	public class RunClassParameters
	{
		public RunClassParameters(string _className, string _methodName, List<Object> _parameters)
		{
			className = _className;
			methodName = _methodName;
			parameters = _parameters;
			isTest = false;
		}

		public string className { get; set; }
		public string methodName { get; set; }
		public List<Object> parameters { get; set; }
		public bool isTest { get; set; }
	}
}

