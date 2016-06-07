// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using LfMerge.Logging;
using LfMerge.Settings;
using Palaso.Progress;
using SIL.FieldWorks.FDO;

namespace LfMerge.Actions
{
	public abstract class Action: IAction
	{
		protected LfMergeSettingsIni Settings { get; set; }
		protected ILogger Logger { get; set; }
		protected IProgress Progress { get; set; }

		private  FdoCache Cache { get; set; }

		#region Action handling
		internal static IAction GetAction(ActionNames actionName)
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

		protected Action(LfMergeSettingsIni settings, ILogger logger)
		{
			Settings = settings;
			Logger = logger;
			Progress = MainClass.Container.Resolve<IProgress>();
		}

		protected abstract ProcessingState.SendReceiveStates StateForCurrentAction { get; }

		protected abstract ActionNames NextActionName { get; }

		protected abstract void DoRun(ILfProject project);

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
			Logger.Notice("Action {0} started", Name);

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
			catch (Exception)
			{
				// An exception during initial clone means we'll want to
				// perform an initial clone next time this project is run
				if (project.IsInitialClone)
					project.State.SRState = ProcessingState.SendReceiveStates.CLONING;
				else if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
				{
					Logger.Error("State going to IDLE");
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;
				}
				Logger.Error("LfMerge exiting due to exception in Action.{0}", Name);
				throw;
			}

			Logger.Notice("Action.{0} finished", Name);
		}

		#endregion
	}
}

