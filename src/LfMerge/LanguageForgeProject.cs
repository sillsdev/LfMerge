﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

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

		private ILfMergeSettings _settings;
		private FwProject _fieldWorksProject;
		private readonly ProcessingState _state;
		private readonly string _projectCode;
		private ILanguageDepotProject _languageDepotProject;

		public static LanguageForgeProject Create(ILfMergeSettings settings, string projectCode)
		{
			LanguageForgeProject project;
			if (CachedProjects.TryGetValue(projectCode, out project))
				return project;

			project = new LanguageForgeProject(settings, projectCode);
			CachedProjects.Add(projectCode, project);
			return project;
		}

		protected LanguageForgeProject(ILfMergeSettings settings, string projectCode)
		{
			_settings = settings;
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
					_fieldWorksProject = new FwProject(_settings, FwProjectCode);
				}
				return _fieldWorksProject;
			}
		}

		public ProcessingState State
		{
			get { return _state; }
		}

		public ILanguageDepotProject LanguageDepotProject
		{
			get
			{
				if (_languageDepotProject == null)
				{
					_languageDepotProject = MainClass.Container.Resolve<ILanguageDepotProject>();
					_languageDepotProject.Initialize(LfProjectCode);
				}
				return _languageDepotProject;
			}
		}

		#endregion
	}
}
