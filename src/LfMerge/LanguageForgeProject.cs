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

		/// <summary>
		/// The prefix prepended to project codes to get the Mongo database name.
		/// </summary>
		public const string MongoDatabaseNamePrefix = "sf_"; // TODO: Should this be in the config?

		private FwProject _fieldWorksProject;
		private readonly ProcessingState _state;
		private readonly string _projectCode;
		private LanguageDepotProject _languageDepotProject;

		public static LanguageForgeProject Create(string projectCode)
		{
			LanguageForgeProject project;
			if (CachedProjects.TryGetValue(projectCode, out project))
				return project;

			project = new LanguageForgeProject(projectCode);
			CachedProjects.Add(projectCode, project);
			return project;
		}

		private LanguageForgeProject(string projectCode)
		{
			_projectCode = projectCode;
			_state = ProcessingState.Deserialize(projectCode);
		}

		#region ILfProject implementation

		public string LfProjectCode { get { return _projectCode; } }

		public string MongoDatabaseName { get { return MongoDatabaseNamePrefix + LfProjectCode; } }

		public FwProject FieldWorksProject
		{
			get
			{
				if (_fieldWorksProject == null)
				{
					// for now we simply use the language forge project code as name for the fwdata file
					_fieldWorksProject = new FwProject(LfProjectCode);
				}
				return _fieldWorksProject;
			}
		}

		public ProcessingState State
		{
			get { return _state; }
		}

		public LanguageDepotProject LanguageDepotProject
		{
			get
			{
				if (_languageDepotProject == null)
				{
					_languageDepotProject = new LanguageDepotProject(LfProjectName);
				}
				return _languageDepotProject;
			}
		}

		#endregion
	}
}

