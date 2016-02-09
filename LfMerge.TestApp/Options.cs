// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using CommandLine;
using LfMerge;

namespace LfMerge.TestApp
{
	public class Options: LfMerge.Options
	{
		[Option('u', "user", HelpText = "LanguageDepot username")]
		public string Username { get; set; }

		[Option('w', "password", HelpText = "LanguageDepot password")]
		public string Password { get; set; }

		[Option("ldproj", HelpText = "LanguageDepot project code")]
		public string LdProjectCode { get; set; }

	}
}

