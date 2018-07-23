// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;

namespace LfMerge.Core.Tools
{
	public class ProjectInUncleanStateException: Exception
	{
		public ProjectInUncleanStateException(string message) : base(message)
		{
		}
	}
}