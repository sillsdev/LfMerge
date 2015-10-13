// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.ComponentModel;
using SIL.FieldWorks.FDO;

namespace LfMerge
{
	public class ConsoleFdoUi: IFdoUI
	{
		private ISynchronizeInvoke _synchronizeInvoke;

		public ConsoleFdoUi(ISynchronizeInvoke synchronizeInvoke)
		{
			_synchronizeInvoke = synchronizeInvoke;
		}

		#region IFdoUI implementation

		public bool ConflictingSave()
		{
			throw new NotImplementedException();
		}

		public bool ConnectionLost()
		{
			throw new NotImplementedException();
		}

		public FileSelection ChooseFilesToUse()
		{
			throw new NotImplementedException();
		}

		public bool RestoreLinkedFilesInProjectFolder()
		{
			throw new NotImplementedException();
		}

		public YesNoCancel CannotRestoreLinkedFilesToOriginalLocation()
		{
			throw new NotImplementedException();
		}

		public void DisplayMessage(MessageType type, string message, string caption, string helpTopic)
		{
			Console.WriteLine("{0}: {1}", type, message);
		}

		public void ReportException(Exception error, bool isLethal)
		{
			Console.WriteLine("Got exception: {0}: {1}\n{2}", error.GetType(), error.Message, error);
		}

		public void ReportDuplicateGuids(string errorText)
		{
			Console.WriteLine("Duplicate GUIDs: " + errorText);
		}

		public bool Retry(string msg, string caption)
		{
			Console.WriteLine(msg);
			return true;
		}

		public bool OfferToRestore(string projectPath, string backupPath)
		{
			throw new NotImplementedException();
		}

		public void Exit()
		{
			Console.WriteLine("Exiting");
		}

		public ISynchronizeInvoke SynchronizeInvoke
		{
			get
			{
				return _synchronizeInvoke;
			}
		}

		public DateTime LastActivityTime
		{
			get
			{
				return DateTime.Now;
			}
		}

		#endregion
	}
}

