// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge.Actions;

namespace LfMerge.Queues
{
	public interface IQueue
	{
		QueueNames Name { get; }

		bool IsEmpty { get; }

		string[] QueuedProjects { get; }

		void EnqueueProject(string projectName);

		void DequeueProject(string projectName);

		IQueue NextQueueWithWork { get; }

		IAction CurrentAction { get; }
	}
}

