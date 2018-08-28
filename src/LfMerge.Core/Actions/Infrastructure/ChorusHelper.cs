// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Autofac;
using SIL.Network;
using LfMerge.Core.Settings;
using SIL.LCModel;

namespace LfMerge.Core.Actions.Infrastructure
{
	public class ChorusHelper
	{
		static ChorusHelper()
		{
			Username = "x";
			Password = "x";
		}

		public virtual string GetSyncUri(ILfProject project)
		{
			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			if (!string.IsNullOrEmpty(settings.LanguageDepotRepoUri))
				return settings.LanguageDepotRepoUri;

			var uriBldr = new UriBuilder(project.LanguageDepotProjectUri) {
				UserName = Username,
				Password = Password,
				Path = HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Identifier)
			};
			return uriBldr.Uri.ToString();
		}

		public int ModelVersion { get; private set; }

		public static void SetModelVersion(int modelVersion)
		{
			var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
			chorusHelper.ModelVersion = modelVersion;
		}

		public static bool RemoteDataIsForDifferentModelVersion
		{
			get
			{
				var chorusHelper = MainClass.Container.Resolve<ChorusHelper>();
				return chorusHelper.ModelVersion != LcmCache.ModelVersion;
			}
		}

		public static string Username { get; set; }
		public static string Password { get; set; }
	}
}
