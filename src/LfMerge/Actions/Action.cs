// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using Autofac;

namespace LfMerge.Actions
{
	public abstract class Action: IAction
	{
		protected ILfMergeSettings Settings { get; set; }

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
			containerBuilder.RegisterType<CommitAction>().Keyed<IAction>(ActionNames.Commit).SingleInstance();
			containerBuilder.RegisterType<MergeAction>().Keyed<IAction>(ActionNames.Merge).SingleInstance();
			containerBuilder.RegisterType<ReceiveAction>().Keyed<IAction>(ActionNames.Receive).SingleInstance();
			containerBuilder.RegisterType<SendAction>().Keyed<IAction>(ActionNames.Send).SingleInstance();
			containerBuilder.RegisterType<UpdateFdoFromMongoDbAction>().Keyed<IAction>(ActionNames.UpdateFdoFromMongoDb).SingleInstance();
			containerBuilder.RegisterType<UpdateMongoDbFromFdo>().Keyed<IAction>(ActionNames.UpdateMongoDbFromFdo).SingleInstance();
		}

		#endregion

		public Action(ILfMergeSettings settings)
		{
			Settings = settings;
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

		public void Run(ILfProject project)
		{
			if (project.State.SRState == ProcessingState.SendReceiveStates.HOLD)
				return;

			project.State.SRState = StateForCurrentAction;
			try
			{
				DoRun(project);
			}
			// REVIEW: catch any exception and set state to hold?
			// TODO: log exceptions
			finally
			{
				if (project.State.SRState != ProcessingState.SendReceiveStates.HOLD)
					project.State.SRState = ProcessingState.SendReceiveStates.IDLE;
			}
		}

		#endregion
	}
}

