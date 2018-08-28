// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.ComponentModel;
using SIL.LCModel;

namespace LfMerge.Core.FieldWorks
{
	class ConsoleLcmUi: ILcmUI
	{
		private ISynchronizeInvoke _synchronizeInvoke;

		public ConsoleLcmUi(ISynchronizeInvoke synchronizeInvoke)
		{
			_synchronizeInvoke = synchronizeInvoke;
		}

		#region ILcmUI implementation

		public void DisplayCircularRefBreakerReport(string msg, string caption)
		{
			MainClass.Logger.Warning(msg);
		}

		public bool ConflictingSave()
		{
			MainClass.Logger.Error("ConsoleLcmUI.ConflictingSave...");
			// Revert to saved state
			return true;
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
			MainClass.Logger.Warning("{0}: {1}", type, message);
		}

		public void ReportException(Exception error, bool isLethal)
		{
			MainClass.Logger.Warning("Got exception: {0}: {1}\n{2}", error.GetType(), error.Message, error);
		}

		public void ReportDuplicateGuids(string errorText)
		{
			MainClass.Logger.Warning("Duplicate GUIDs: " + errorText);
		}

		public bool Retry(string msg, string caption)
		{
			Console.WriteLine(msg);
			return true;
		}

		public bool OfferToRestore(string projectPath, string backupPath)
		{
			return false;
		}

		public void Exit()
		{
			MainClass.Logger.Debug("Exiting");
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

