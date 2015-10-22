// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;

namespace LfMerge.Queues
{
	public class Queue: IQueue
	{
		#region Queue handling
		private static IQueue[] _queues;

		static Queue()
		{
			var values = Enum.GetValues(typeof(QueueNames));
			_queues = new IQueue[values.Length];
			foreach (QueueNames queueName in values)
			{
				_queues[(int)queueName] = (queueName == QueueNames.None) ? null : new Queue(queueName);
			}
		}

		public static IQueue NextQueueWithWork(Actions currentAction)
		{
			bool firstLoop = true;
			var action = currentAction;
			while ((action != currentAction && !Options.Current.StopAfterFirstAction) || firstLoop)
			{
				firstLoop = false;
				var queueName = Options.GetQueueForAction(action);
				if (queueName != QueueNames.None)
				{
					var queue = _queues[(int)queueName];
					if (!queue.IsEmpty)
						return queue;
				}
				action = Options.Current.GetNextAction(action);
			}
			return null;
		}
		#endregion

		public Queue(QueueNames name)
		{
			if (name == QueueNames.None)
				throw new ArgumentException("Can't create a queue of type QueueNames.None", "name");

			Name = name;
		}

		#region IQueue implementation
		public bool IsEmpty
		{
			get { return QueuedProjects.Length == 0; }
		}

		public QueueNames Name { get; private set; }

		public string[] QueuedProjects
		{
			get
			{
				return new DirectoryInfo(QueueDirectory).GetFiles()
					.OrderBy(f => f.CreationTimeUtc)
					.Select(f => f.Name).ToArray();
			}
		}
		#endregion

		private string QueueDirectory
		{
			get { return LfMergeDirectories.Current.GetQueueDirectory(Name); }
		}
	}
}

