// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge.Core;
using LfMerge.Core.Settings;

namespace LfMerge.Core.Tests
{
	public class LanguageDepotMock : LanguageForgeProject
	{
		public LanguageDepotMock(LfMergeSettings settings, string projectCode)
			: base(settings, projectCode)
		{
		}
	}
}
