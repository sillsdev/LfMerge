// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.Core.Actions;

namespace LfMerge.Core.Queues
{
	public interface IQueue
	{
		QueueNames Name { get; }

		bool IsEmpty { get; }

		string[] QueuedProjects { get; }

		void EnqueueProject(string projectCode);

		void DequeueProject(string projectCode);

		IQueue NextQueueWithWork { get; }

		ActionNames CurrentActionName { get; }
	}
}

