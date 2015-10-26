// Copyright (c) 2011-2015 SIL International
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

		private QueueNames FirstQueue
		{
			get
			{
				return SingleQueue != QueueNames.None ? SingleQueue :
					PriorityQueue != QueueNames.None ? PriorityQueue : QueueNames.Merge;
			}
		}

		public ActionNames FirstAction
		{
			get { return GetActionForQueue(FirstQueue); }
		}

		public bool StopAfterFirstAction
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

		public ActionNames GetNextAction(ActionNames currentAction)
		{
			int nextAction = 0;
			if (!StopAfterFirstAction)
				nextAction = ((int)currentAction) + 1;

			if (nextAction > (int)ActionNames.UpdateMongoDbFromFdo)
				nextAction = 0;
			return (ActionNames)nextAction;
		}

		public static ActionNames GetActionForQueue(QueueNames queue)
		{
			switch (queue)
			{
				case QueueNames.Commit:
					return ActionNames.Commit;
				case QueueNames.Merge:
					return ActionNames.UpdateFdoFromMongoDb;
				case QueueNames.None:
					break;
				case QueueNames.Receive:
					return ActionNames.Receive;
				case QueueNames.Send:
					return ActionNames.Send;
			}
			return ActionNames.None;
		}

		public static QueueNames GetQueueForAction(ActionNames action)
		{
			switch (action)
			{
				case ActionNames.UpdateFdoFromMongoDb:
					return QueueNames.Merge;
				case ActionNames.Commit:
					return QueueNames.Commit;
				case ActionNames.Receive:
					return QueueNames.Receive;
				case ActionNames.Send:
					return QueueNames.Send;
				case ActionNames.None:
				case ActionNames.Merge:
				case ActionNames.UpdateMongoDbFromFdo:
					break;
			}
			return QueueNames.None;
		}

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			if (Parser.Default.ParseArguments(args, options))
			{
				if (options.AllArgumentsValid && !options.ShowHelp)
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

