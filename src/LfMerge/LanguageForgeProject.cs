// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using LfMerge.FieldWorks;

namespace LfMerge
{
	public class LanguageForgeProject: ILfProject
	{
		private FwProject _fieldWorksProject;
		private readonly ProcessingState _state;
		private readonly string _projectName;

		public LanguageForgeProject(string projectName)
		{
			_projectName = projectName;
			_state = new ProcessingState(projectName);
		}

		#region ILfProject implementation

		public string LfProjectName { get { return _projectName; } }

		public FwProject FieldWorksProject
		{
			get
			{
				if (_fieldWorksProject == null)
				{
					// for now we simply use the language forge project name as name for the fwdata file
					_fieldWorksProject = new FwProject(LfProjectName);
				}
				return _fieldWorksProject;
			}
		}

		public ProcessingState State
		{
			get { return _state; }
		}

		#endregion
	}
}

