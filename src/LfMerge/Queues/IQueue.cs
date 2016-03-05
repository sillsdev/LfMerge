// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.Actions;

namespace LfMerge.Queues
{
	public interface IQueue
	{
		QueueNames Name { get; }

		bool IsEmpty { get; }

		string[] QueuedProjects { get; }

		void EnqueueProject(string projectCode);

		void DequeueProject(string projectCode);

		IQueue NextQueueWithWork { get; }

		IAction CurrentAction { get; }
	}
}

