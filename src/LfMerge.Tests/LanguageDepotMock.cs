// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge;
using LfMerge.Settings;
using System;

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
