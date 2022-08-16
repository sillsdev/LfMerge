// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using CommandLine;
using LfMerge.Core;

namespace LfMerge.QueueManager
{
	public class QueueManagerOptions : OptionsBase<QueueManagerOptions>
	{
		public QueueManagerOptions()
		{
			Current = this;
		}

		[Option('p', "project", HelpText = "Process the specified project first")]
		public string PriorityProject { get; set; }
	}
}
