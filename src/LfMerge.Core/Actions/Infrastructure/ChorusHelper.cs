// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using Palaso.Network;
using SIL.FieldWorks.FDO;
using LfMerge.Core.Settings;

namespace LfMerge.Core.Actions.Infrastructure
{
	public class ChorusHelper
	{
		public virtual string GetSyncUri(ILfProject project)
		{
			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			if (!string.IsNullOrEmpty(settings.LanguageDepotRepoUri))
				return settings.LanguageDepotRepoUri;

			var uriBldr = new UriBuilder(project.LanguageDepotProjectUri);
			uriBldr.UserName = "x";
			uriBldr.Password = "x";
			uriBldr.Path = HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Identifier);
			return uriBldr.Uri.ToString();
		}

		public string ModelVersion { get; private set; }
		public bool TreatAsInitialClone { get; private set; }

		public static void SetModelVersion(string modelVersion)
		{
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			chorusHelper.ModelVersion = modelVersion;
		}

		public static void SetTreatAsInitialClone(bool treatAsInitialClone)
		{
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			chorusHelper.TreatAsInitialClone = treatAsInitialClone;
		}

		public static bool ThisIsAnInitialClone
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return chorusHelper.TreatAsInitialClone;
			}
		}

		public static bool RemoteDataIsForDifferentModelVersion
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return !string.IsNullOrEmpty(chorusHelper.ModelVersion) &&
					chorusHelper.ModelVersion != FdoCache.ModelVersion;
			}
		}
	}
}
