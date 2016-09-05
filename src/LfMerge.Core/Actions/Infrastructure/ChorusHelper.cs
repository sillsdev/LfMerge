// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Palaso.Network;

namespace LfMerge.Core.Actions.Infrastructure
{
	public class ChorusHelper
	{
		public virtual string GetSyncUri(ILfProject project)
		{
			string uri = project.LanguageDepotProjectUri;
			string serverPath = uri.StartsWith("http://") ? uri.Replace("http://", "") : uri;
			return "http://x:x@" + serverPath + "/" +
				HttpUtilityFromMono.UrlEncode(project.LanguageDepotProject.Identifier);
		}
	}
}
