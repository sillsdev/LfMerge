// Copyright (c) 2016 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.ComponentModel;
using SIL.Utils;

namespace LfMerge.FieldWorks
{
	class ThreadedProgress: IThreadedProgress
	{
		private SingleThreadedSynchronizeInvoke _synchronizeInvoke = new SingleThreadedSynchronizeInvoke();

		#region IProgress implementation

		#pragma warning disable 67 // The event `LfMerge.ThreadedProgress.Canceling' is never used
		public event CancelEventHandler Canceling; // this is part of the interface
		#pragma warning restore 67

		public void Step(int amount)
		{
		}

		public string Title { get; set; }
		public string Message { get; set; }
		public int Position { get; set; }
		public int StepSize { get; set; }
		public int Minimum { get; set; }
		public int Maximum { get; set; }
		public ISynchronizeInvoke SynchronizeInvoke
		{
			get
			{
				return _synchronizeInvoke;
			}
		}

		public bool IsIndeterminate { get; set; }
		public bool AllowCancel { get; set; }
		#endregion

		#region IThreadedProgress implementation

		public object RunTask(Func<IThreadedProgress, object[], object> backgroundTask, params object[] parameters)
		{
			return _synchronizeInvoke.Invoke(backgroundTask, parameters);
		}

		public object RunTask(bool fDisplayUi, Func<IThreadedProgress, object[], object> backgroundTask, params object[] parameters)
		{
			return _synchronizeInvoke.Invoke(backgroundTask, parameters);
		}

		public bool Canceled
		{
			get
			{
				return false;
			}
		}

		#endregion
	}
}

