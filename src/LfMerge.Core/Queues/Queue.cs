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
				containerBuilder.RegisterType<Queue>();
		}

		public static IQueue GetQueue()
		{
			return MainClass.Container.Resolve<Queue>();
		}

		#endregion

		public Queue(LfMergeSettings settings)
		{
			Settings = settings;
		}

		#region IQueue implementation
		public bool IsEmpty
		{
			get { return QueuedProjects.Length == 0; }
		}

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

		public ActionNames CurrentActionName
		{
			get { return ActionNames.Synchronize; }
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
			get { return Settings.GetQueueDirectory(); }
		}
	}
}

