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

		[Option("clone", HelpText = "Clone the specified project if needed")]
		public bool CloneProject { get; set; }

		[HelpOption('h', "help")]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		[ParserState]
		public IParserState LastParserState { get; set; }

		public ActionNames FirstAction
		{
			get { return GetActionForQueue(QueueNames.Edit); }
		}

		// REVIEW: improve naming of this method. This method returns the next action for the
		// purpose of enumerating over all actions. It doesn't return the next action that should
		// logically be run. That is returned by IAction.NextAction.
		public ActionNames GetNextAction(ActionNames currentAction)
		{
			int nextAction = ((int)currentAction) + 1;

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
				Current = options;
				return options;
			}
			// CommandLineParser automagically handles displaying help
			return null;
		}
	}
}
