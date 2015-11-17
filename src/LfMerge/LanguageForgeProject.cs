// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Autofac;
using LfMerge.FieldWorks;

namespace LfMerge
{
	public class LanguageForgeProject: ILfProject
	{
		protected static Dictionary<string, LanguageForgeProject> CachedProjects =
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

		protected LanguageForgeProject(string projectCode)
		{
			_projectCode = projectCode;
			_state = ProcessingState.Deserialize(projectCode);
		}

		public static void DisposeProjectCache()
		{
			foreach (LanguageForgeProject project in CachedProjects.Values)
			{
				if (project._fieldWorksProject != null)
					project._fieldWorksProject.Dispose();
			}
			CachedProjects.Clear();
		}

		#region ILfProject implementation

		// TODO: ToLowerInvariant() won't necessarily be right in all cases. Find a better way.
		public string LfProjectCode { get { return _projectCode.ToLowerInvariant(); } }
		public string FwProjectCode { get { return _projectCode; } }

		public string MongoDatabaseName { get { return MongoDatabaseNamePrefix + LfProjectCode; } }

		public FwProject FieldWorksProject
		{
			get
			{
				if (_fieldWorksProject == null)
				{
					// for now we simply use the language forge project code as name for the fwdata file
					_fieldWorksProject = new FwProject(FwProjectCode);
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
					_languageDepotProject = new LanguageDepotProject(LfProjectCode);
				}
				return _languageDepotProject;
			}
		}

		#endregion
	}
}

