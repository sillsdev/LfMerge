// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using CommandLine;
using CommandLine.Text;

namespace LfMerge
{
	public class Options
	{
		public enum QueueNames
		{
			None,
			Merge,
			Send,
			Receive,
			Commit
		}

		[Option("priority-queue", HelpText = "Queue to process first (merge|send|receive|commit)")]
		public QueueNames PriorityQueue { get; set; }

		[Option('q', "queue", HelpText = "Only process the specified queue")]
		public QueueNames SingleQueue { get; set; }

		[Option("priority-project", DefaultValue = "all", HelpText = "Project to process first")]
		public string PriorityProject { get; set; }

		[Option('p', "project", HelpText = "Only process the specified project")]
		public string SingleProject { get; set; }

		[Option('h', "help", HelpText = "Display this help")]
		public bool ShowHelp { get; set; }

		public string GetUsage()
		{
			var help = new HelpText
			{
				Heading = new HeadingInfo("LfMerge"),
				Copyright = new CopyrightInfo("SIL International", 2015),
				AdditionalNewLineAfterOption = false,
				AddDashesToOption = true
			};
			help.AddOptions(this);
			return help;
		}

		public string FirstProject
		{
			get { return string.IsNullOrEmpty(SingleProject) ? PriorityProject : SingleProject; }
		}

		public bool StopAfterFirstProject
		{
			get { return !string.IsNullOrEmpty(SingleProject); }
		}

		public QueueNames FirstQueue
		{
			get
			{
				return SingleQueue != QueueNames.None ? SingleQueue :
					PriorityQueue != QueueNames.None ? PriorityQueue : QueueNames.Merge;
			}
		}

		public bool StopAfterFirstQueue
		{
			get { return SingleQueue != QueueNames.None; }
		}

		private bool AllArgumentsValid
		{
			get
			{
				return ((PriorityProject == "all" || string.IsNullOrEmpty(SingleProject)) &&
					(PriorityQueue == QueueNames.None || SingleQueue == QueueNames.None));
			}
		}

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			if (Parser.Default.ParseArguments(args, options))
			{
				if (options.AllArgumentsValid && !options.ShowHelp)
					return options;
			}
			// Display the default usage information
			Console.WriteLine(options.GetUsage());
			return null;
		}
	}
}

