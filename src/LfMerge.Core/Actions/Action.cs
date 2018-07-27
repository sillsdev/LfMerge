// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using SIL.Progress;
using SIL.FieldWorks.FDO;
using LfMerge.Core.Queues;
using System.Collections.Generic;

namespace LfMerge.Core.Actions
{
	public abstract class Action: IAction
	{
		protected LfMergeSettings Settings { get; set; }
		protected ILogger Logger { get; set; }
		protected IProgress Progress { get; set; }

		private  FdoCache Cache { get; set; }

		#region Action handling
		public static IAction GetAction(ActionNames actionName)
		{
			var action = MainClass.Container.ResolveKeyed<IAction>(actionName);
			var actionAsAction = action as Action;
			if (actionAsAction != null)
				actionAsAction.Name = actionName;
			return action;
		}

		internal static void Register(ContainerBuilder containerBuilder)
		{
			containerBuilder.RegisterType<EnsureCloneAction>().Keyed<IAction>(ActionNames.EnsureClone).SingleInstance();
			containerBuilder.RegisterType<CommitAction>().Keyed<IAction>(ActionNames.Commit).SingleInstance();
			containerBuilder.RegisterType<EditAction>().Keyed<IAction>(ActionNames.Edit).SingleInstance();
			containerBuilder.RegisterType<SynchronizeAction>().Keyed<IAction>(ActionNames.Synchronize).SingleInstance();
			containerBuilder.RegisterType<TransferMongoToFdoAction>().Keyed<IAction>(ActionNames.TransferMongoToFdo).SingleInstance();
			containerBuilder.RegisterType<TransferFdoToMongoAction>().Keyed<IAction>(ActionNames.TransferFdoToMongo).SingleInstance();
		}

		#endregion

		protected Action(LfMergeSettings settings, ILogger logger)
		{
			Settings = settings;
			Logger = logger;
			Progress = MainClass.Container.Resolve<IProgress>();
		}

		protected abstract ProcessingState.SendReceiveStates StateForCurrentAction { get; }

		protected abstract ActionNames NextActionName { get; }

		protected abstract void DoRun(ILfProject project);

		public static ActionNames FirstActionName
		{
			get { return GetActionNameForQueue(QueueNames.Edit); }
		}

		public static ActionNames GetActionNameForQueue(QueueNames queue)
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

		internal static IEnumerable<ActionNames> EnumerateActionsStartingWith(ActionNames currentAction)
		{
			yield return currentAction;
			var action = currentAction;

			do
			{
				action++;
				if (action > ActionNames.TransferFdoToMongo)
					action = 0;
				yield return action;
			} while (action != currentAction);

			yield return ActionNames.None;
		}

		#region IAction implementation

		public ActionNames Name { get; private set; }

		public IAction NextAction
		{
			get
			{
				return NextActionName != ActionNames.None ? GetAction(NextActionName) : null;
			}
		}

		public virtual void PreRun(ILfProject project)
		{
			// Default implementation does nothing
		}

		public void Run(ILfProject project)
		{
			Logger.Notice("Action.{0} started", Name);

			PreRun(project);

			if (project.State.SRState == ProcessingState.SendReceiveStates.HOLD)
			{
				Logger.Notice("LFMerge on hold");
				return;
			}

			project.State.SRState = StateForCurrentAction;
			try
			{
				DoRun(project);
			}
			catch (Exception e)
			{
				// An exception during initial clone means we'll want to
				// perform an initial clone next time this project is run
				if (project.IsInitialClone)
					project.State.SRState = ProcessingState.SendReceiveStates.CLONING;
				else if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD &&
					project.State.SRState != ProcessingState.SendReceiveStates.ERROR)
				{
					Logger.Error("Got exception. State going to IDLE");
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;
				}
				Logger.Error("LfMerge exiting due to {1} exception in Action.{0} ({2})", Name,
					e.GetType(), e.Message);
				if (ExceptionLogging.Client != null) // can be null when running unit tests
					ExceptionLogging.Client.Notify(e, Bugsnag.Payload.HandledState.ForHandledException());
				throw;
			}

			Logger.Notice("Action.{0} finished", Name);
		}

		#endregion
	}
}

