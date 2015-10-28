// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.FieldWorks;

namespace LfMerge
{
	public class LanguageForgeProject: ILfProject
	{
		private static Dictionary<string, LanguageForgeProject> CachedProjects =
			new Dictionary<string, LanguageForgeProject>();

		private FwProject _fieldWorksProject;
		private readonly ProcessingState _state;
		private readonly string _projectName;

		public static LanguageForgeProject Create(string projectName)
		{
			LanguageForgeProject project;
			if (CachedProjects.TryGetValue(projectName, out project))
				return project;

			project = new LanguageForgeProject(projectName);
			CachedProjects.Add(projectName, project);
			return project;
		}

		private LanguageForgeProject(string projectName)
		{
			_projectName = projectName;
			_state = ProcessingState.Deserialize(projectName);
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

