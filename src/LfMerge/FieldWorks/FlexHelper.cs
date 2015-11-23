// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using SIL.Progress;
using LibFLExBridgeChorusPlugin;

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

