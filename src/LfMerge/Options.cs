// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using CommandLine;
using CommandLine.Text;
using LfMerge.Queues;
using LfMerge.Actions;

namespace LfMerge
{
	public class Options
	{
		public static Options Current;

		public Options()
		{
			Current = this;
		}

		[Option('p', "project", HelpText = "Process the specified project first")]
		public string PriorityProject { get; set; }

		[Option('h', "help", HelpText = "Display this help")]
		public bool ShowHelp { get; set; }

		public string GetUsage()
		{
			var help = new HelpText
			{
				Heading = new HeadingInfo("LfMerge"),
				Copyright = new CopyrightInfo("SIL International", 2016),
				AdditionalNewLineAfterOption = false,
				AddDashesToOption = true
			};
			help.AddOptions(this);
			return help;
		}

		public string FirstProject
		{
			get { return string.IsNullOrEmpty(PriorityProject) ? PriorityProject : PriorityProject; }
		}

		public bool StopAfterFirstProject
		{
			get { return false; }
		}

		public ActionNames FirstAction
		{
			get { return GetActionForQueue(QueueNames.Edit); }
		}

		public bool StopAfterFirstAction
		{
			get { return false; }
		}

		private bool AllArgumentsValid(string[] args)
		{
			return (!string.IsNullOrEmpty(PriorityProject) ||
				(args == null) ||
				(args.Length == 0));
		}

		public ActionNames GetNextAction(ActionNames currentAction)
		{
			int nextAction = 0;
			if (!StopAfterFirstAction)
				nextAction = ((int)currentAction) + 1;

			if (nextAction > (int)ActionNames.TransferFdoToMongo)
				nextAction = 0;
			return (ActionNames)nextAction;
		}

		public static ActionNames GetActionForQueue(QueueNames queue)
		{
			switch (queue)
			{
				case QueueNames.Edit:
					return ActionNames.Edit;
				case QueueNames.None:
					break;
				case QueueNames.Synchronize:
					return ActionNames.Synchronize;
			}
			return ActionNames.None;
		}

		public static QueueNames GetQueueForAction(ActionNames action)
		{
			switch (action)
			{
				case ActionNames.TransferMongoToFdo:
				case ActionNames.Synchronize:
					return QueueNames.Synchronize;
				case ActionNames.Commit:
				case ActionNames.None:
				case ActionNames.Edit:
				case ActionNames.TransferFdoToMongo:
					break;
			}
			return QueueNames.None;
		}

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			if (Parser.Default.ParseArguments(args, options))
			{
				if (options.AllArgumentsValid(args) && !options.ShowHelp)
				{
					Current = options;
					return options;
				}
			}
			// Display the default usage information
			Console.WriteLine(options.GetUsage());
			return null;
		}
	}
}
