// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge;
using LfMerge.Settings;

namespace LfMerge.Tests
{
	public class LanguageDepotMock : LanguageForgeProject
	{
		public LanguageDepotMock(LfMergeSettingsIni settings, string projectCode)
			: base(settings, projectCode)
		{
		}
	}
}
