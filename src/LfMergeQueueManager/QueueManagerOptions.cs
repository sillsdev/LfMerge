// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using CommandLine;
using CommandLine.Text;

namespace LfMerge.QueueManager
{
	public class QueueManagerOptions
	{
		public static QueueManagerOptions Current;

		public QueueManagerOptions()
		{
			Current = this;
		}

		[Option('p', "project", HelpText = "Process the specified project first")]
		public string PriorityProject { get; set; }

		[HelpOption('h', "help")]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		[ParserState]
		public IParserState LastParserState { get; set; }


		public static QueueManagerOptions ParseCommandLineArgs(string[] args)
		{
			var options = new QueueManagerOptions();
			if (Parser.Default.ParseArguments(args, options))
			{
				Current = options;
				return options;
			}
			// CommandLineParser automagically handles displaying help
			return null;
		}
	}
}
