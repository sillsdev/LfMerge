// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using CommandLine;
using CommandLine.Text;
using LfMerge.Queues;

namespace LfMerge.TestApp
{
	public class Options
	{
		[Option('q', "queue", Required = true, HelpText = "Queue to set")]
		public QueueNames QueueName { get; set; }

		[Option('p', "project", Required = true, HelpText = "Input file to read.")]
		public string ProjectCode { get; set; }

		[Option('u', "user", HelpText = "LanguageDepot username")]
		public string Username { get; set; }

		[Option('w', "password", HelpText = "LanguageDepot password")]
		public string Password { get; set; }

		[Option("ldproj", HelpText = "LanguageDepot project code")]
		public string LdProjectCode { get; set; }

		[Option('h', "help", HelpText = "Display this help")]
		public bool ShowHelp { get; set; }

		public string GetUsage()
		{
			var help = new HelpText
			{
				Heading = new HeadingInfo("LfMerge.TestApp"),
				Copyright = new CopyrightInfo("SIL International", 2015),
				AdditionalNewLineAfterOption = false,
				AddDashesToOption = true
			};
			help.AddOptions(this);
			return help;
		}


	}
}

