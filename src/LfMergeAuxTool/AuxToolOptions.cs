// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using CommandLine;
using CommandLine.Text;

namespace LfMergeAuxTool
{
	public class AuxToolOptions
	{
		[Option('p', "project", Required = true, HelpText = "Path to fwdata file to process.")]
		public string Project { get; set; }

		[Option('c', "commit", HelpText = "Commit the current fwdata file to the .hg repo")]
		public bool Commit { get; set; }

		[Option('i', "info", HelpText = "Display database model version of project")]
		public bool InfoOnly { get; set; }

		[Option('m', "migrate", HelpText = "Migrate project to current model version")]
		public bool Migrate { get; set; }

		[HelpOption('h', "help")]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this,
				current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		[ParserState]
		public IParserState LastParserState { get; set; }

		public static AuxToolOptions ParseCommandLineArgs(string[] args)
		{
			var options = new AuxToolOptions();
			if (Parser.Default.ParseArguments(args, options))
			{
				return options;
			}
			// CommandLineParser automagically handles displaying help
			return null;
		}
	}
}

