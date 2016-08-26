// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.Settings;

namespace LfMerge.Core.Queues
{
	public class Queue: IQueue
	{
		private LfMergeSettings Settings { get; set; }

		#region Queue handling
		internal static void Register(ContainerBuilder containerBuilder)
		{
			foreach (QueueNames queueName in Enum.GetValues(typeof(QueueNames)))
			{
				if (queueName == QueueNames.None)
					continue;

				containerBuilder.RegisterType<Queue>().Keyed<IQueue>(queueName)
					.WithParameter(new TypedParameter(typeof(QueueNames), queueName));
			}
		}

		public static IQueue GetQueue(QueueNames name)
		{
			return MainClass.Container.ResolveKeyed<IQueue>(name);
		}

		public static IQueue FirstQueueWithWork
		{
			get { return GetNextQueueWithWork(Actions.Action.FirstAction); }
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

		public static IQueue GetNextQueueWithWork(ActionNames currentAction)
		{
			foreach (var action in Actions.Action.EnumerateActionsStartingWith(currentAction))
			{
				var queueName = GetQueueForAction(action);
				if (queueName != QueueNames.None)
				{
					var queue = GetQueue(queueName);
					if (!queue.IsEmpty)
						return queue;
				}
			}
			return null;
		}

		public static void CreateQueueDirectories(LfMergeSettings settings)
		{
			foreach (QueueNames queueName in Enum.GetValues(typeof(QueueNames)))
			{
				var queueDir = settings.GetQueueDirectory(queueName);
				if (queueDir != null)
					Directory.CreateDirectory(queueDir);
			}
		}

		#endregion

		public Queue(LfMergeSettings settings, QueueNames name)
		{
			if (name == QueueNames.None)
				throw new ArgumentException("Can't create a queue of type QueueNames.None", "name");

			Settings = settings;
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
			get { return QueuedProjectsEnumerable().ToArray(); }
		}

		public void EnqueueProject(string projectCode)
		{
			File.WriteAllText(Path.Combine(QueueDirectory, projectCode), string.Empty);
		}

		public void DequeueProject(string projectCode)
		{
			File.Delete(Path.Combine(QueueDirectory, projectCode));
		}

		public IQueue NextQueueWithWork
		{
			get { return GetNextQueueWithWork(Actions.Action.GetActionForQueue(Name)); }
		}

		public IAction CurrentAction
		{
			get { return Actions.Action.GetAction(Actions.Action.GetActionForQueue(Name)); }
		}
		#endregion

		protected virtual string[] RawQueuedProjects
		{
			get
			{
				return new DirectoryInfo(QueueDirectory).GetFiles()
					.OrderBy(f => f.CreationTimeUtc)
					.Select(f => f.Name).ToArray();
			}
		}

		private IEnumerable<string> QueuedProjectsEnumerable(string prioProj = null)
		{
			var projects = RawQueuedProjects.ToList();
			if (!string.IsNullOrEmpty(prioProj) && projects.Contains(prioProj))
			{
				// return priority project first, then loop around for the other projects
				var firstProjIndex = projects.IndexOf(prioProj);
				for (int i = 0; i < projects.Count; i++)
				{
					if (i >= firstProjIndex)
						yield return projects[i];
				}
				for (int i = 0; i < firstProjIndex; i++)
				{
					yield return projects[i];
				}
			}
			else
			{
				// return all projects, starting with oldest
				foreach (var proj in projects)
				{
					yield return proj;
				}
			}
		}

		private string QueueDirectory
		{
			get { return Settings.GetQueueDirectory(Name); }
		}
	}
}

