// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.Core.Settings;

namespace LfMerge.Core.Tests
{
	public class LanguageDepotMock : LanguageForgeProject
	{
		public LanguageDepotMock(string projectCode, LfMergeSettings settings)
			: base(projectCode, settings)
		{
		}

		public static string ProjectFolderPath { get; set; }

		public static MercurialServer Server { get; set; }

	}
}
