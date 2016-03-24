// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Palaso.Network;

namespace LfMerge.Actions.Infrastructure
{
	public class ChorusHelper
	{
		public virtual string GetSyncUri(ILfProject project)
		{
			string uri = project.LanguageDepotProjectUri;
			string serverPath = uri.StartsWith("http://") ? uri.Replace("http://", "") : uri;
			return "http://" +
				HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Username) + ":" +
				HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Password) + "@" + serverPath + "/" +
				HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Identifier);
		}
	}
}
