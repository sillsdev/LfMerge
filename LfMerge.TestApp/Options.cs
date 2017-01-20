// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using CommandLine;

namespace LfMerge.TestApp
{
	public class Options: LfMerge.Options
	{
		[Option("ldproj", HelpText = "LanguageDepot project code")]
		public string LdProjectCode { get; set; }

	}
}

