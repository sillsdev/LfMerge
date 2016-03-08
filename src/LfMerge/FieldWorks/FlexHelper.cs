// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LibFLExBridgeChorusPlugin;
using Palaso.Progress;

namespace LfMerge.FieldWorks
{
	public class FlexHelper
	{
		public virtual void PutHumptyTogetherAgain(IProgress progress, bool verbose, string mainFilePathname)
		{
			FLEx.ProjectUnifier.PutHumptyTogetherAgain(progress, verbose, mainFilePathname);
		}
	}
}

