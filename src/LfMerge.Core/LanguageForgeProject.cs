// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using System.IO;
using Autofac;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Settings;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core
{
	public class LanguageForgeProject: ILfProject
	{
		protected static Dictionary<string, LanguageForgeProject> CachedProjects =
			new Dictionary<string, LanguageForgeProject>();

		private LfMergeSettings _settings;
		private FwProject _fieldWorksProject;
		private readonly ProcessingState _state;
		private readonly string _projectCode;
		private ILanguageDepotProject _languageDepotProject;

		public static LanguageForgeProject Create(string projectCode)
		{
			LanguageForgeProject project;
			if (CachedProjects.TryGetValue(projectCode, out project))
				return project;

			project = new LanguageForgeProject(projectCode);
			CachedProjects.Add(projectCode, project);
			return project;
		}

		protected LanguageForgeProject(string projectCode, LfMergeSettings settings = null)
		{
			_settings = settings ?? MainClass.Container.Resolve<LfMergeSettings>();
			_projectCode = projectCode.ToLowerInvariant();
			_state = ProcessingState.Deserialize(projectCode);
			IsInitialClone = false;
		}

		public static void DisposeProjectCache()
		{
			foreach (LanguageForgeProject project in CachedProjects.Values)
				DisposeFwProject(project);
			CachedProjects.Clear();
		}

		public static void DisposeProjectCache(string projectCode)
		{
			LanguageForgeProject project;
			if (CachedProjects.TryGetValue(projectCode, out project))
			{
				DisposeFwProject(project);
				CachedProjects.Remove(projectCode);
			}
		}

		public static void DisposeFwProject(ILfProject project)
		{
			var lfProject = project as LanguageForgeProject;
			if (lfProject != null && lfProject._fieldWorksProject != null)
			{
				lfProject._fieldWorksProject.Dispose();
				lfProject._fieldWorksProject = null;
			}
		}

		#region ILfProject implementation

		public string ProjectCode { get { return _projectCode; } }

		public string ProjectDir { get { return Path.Combine(_settings.WebWorkDirectory, ProjectCode); }}

		public string FwDataPath
		{
			get
			{
				return Path.Combine(ProjectDir, string.Format("{0}{1}", ProjectCode,
					FdoFileHelper.ksFwDataXmlFileExtension));
			}
		}

		public string MongoDatabaseName { get { return _settings.MongoDatabaseNamePrefix + ProjectCode; } }

		public FwProject FieldWorksProject
		{
			get
			{
				if (_fieldWorksProject == null || _fieldWorksProject.IsDisposed)
				{
					// for now we simply use the language forge project code as name for the fwdata file
					_fieldWorksProject = new FwProject(_settings, ProjectCode);
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
					_languageDepotProject.Initialize(ProjectCode);
				}
				return _languageDepotProject;
			}
		}

		public string LanguageDepotProjectUri
		{
			get
			{
				string hostname = System.Environment.GetEnvironmentVariable("LD_HG_PUBLIC_HOSTNAME") ?? "hg-public.languagedepot.org";
				string protocol = System.Environment.GetEnvironmentVariable("LD_HG_PROTOCOL") ?? "https";
				if (LanguageDepotProject.Repository != null && LanguageDepotProject.Repository.Contains("private"))
					hostname = System.Environment.GetEnvironmentVariable("LD_HG_PRIVATE_HOSTNAME") ?? hostname.Replace("public", "private");
				return string.Format("{0}://{1}", protocol, hostname);
			}
		}

		public bool IsInitialClone
		{
			get;
			set;
		}

		#endregion
	}
}

