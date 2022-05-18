// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using CommandLine;
using CommandLine.Text;
using LfMerge.Core.Actions;

namespace LfMerge
{
	public class Options
	{
		public static Options Current;

		public Options()
		{
			Current = this;
		}

		[Option('p', "project", HelpText = "Process the specified project")]
		public string ProjectCode { get; set; }

		[Option("clone", HelpText = "Clone the specified project if needed")]
		public bool CloneProject { get; set; }

		[Option("action", HelpText = "The action to perform")]
		public ActionNames CurrentAction { get; set; }

		[Option("migrate", HelpText = "Allow data migration")]
		public bool AllowDataMigration { get; set; }

		[Option("user", HelpText = "LanguageDepot username (for debugging purposes only)", DefaultValue = "x")]
		public string User { get; set; }

		[Option("password", HelpText = "LanguageDepot password (for debugging purposes only)", DefaultValue = "x")]
		public string Password { get; set; }

		[HelpOption('h', "help")]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		[ParserState]
		public IParserState LastParserState { get; set; }

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			var parser = ParserInstance ?? Parser.Default;
			if (parser.ParseArguments(args, options))
			{
				Current = options;
				return options;
			}
			// CommandLineParser automagically handles displaying help
			return null;
		}

		/// <summary>
		/// Gets or sets the parser.
		/// </summary>
		/// <remarks>Used in tests. If not set the default parser is used.</remarks>
		public static Parser ParserInstance { get; set; }
	}
}
