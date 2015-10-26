// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Actions
{
	public abstract class Action: IAction
	{
		#region Action handling
		private static IAction[] Actions { get; set; }

		static Action()
		{
			var values = Enum.GetValues(typeof(ActionNames));
			Actions = new IAction[values.Length];
			foreach (ActionNames actionName in values)
			{
				Type actionType = null;
				switch (actionName)
				{
					case ActionNames.None:
						Actions[(int)actionName] = null;
						continue;
					case ActionNames.UpdateFdoFromMongoDb:
						actionType = typeof(UpdateFdoFromMongoDbAction);
						break;
					case ActionNames.Commit:
						actionType = typeof(CommitAction);
						break;
					case ActionNames.Receive:
						actionType = typeof(ReceiveAction);
						break;
					case ActionNames.Merge:
						actionType = typeof(MergeAction);
						break;
					case ActionNames.Send:
						actionType = typeof(SendAction);
						break;
					case ActionNames.UpdateMongoDbFromFdo:
						actionType = typeof(UpdateMongoDbFromFdo);
						break;
				}
				var action = Activator.CreateInstance(actionType) as Action;
				action.Name = actionName;
				Actions[(int)actionName] = action;
			}
		}

		internal static IAction GetAction(ActionNames actionName)
		{
			return Actions[(int)actionName];
		}
		#endregion

		protected Action()
		{
		}

		protected abstract ActionNames NextActionName { get; }

		#region IAction implementation

		public ActionNames Name { get; private set; }

		public IAction NextAction
		{
			get
			{
				return NextActionName != ActionNames.None ? GetAction(NextActionName) : null;
			}
		}

		public abstract void Run(ILfProject project);

		#endregion
	}
}

