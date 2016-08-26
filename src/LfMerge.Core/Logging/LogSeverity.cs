// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.Logging
{
	public enum LogSeverity
	{
		// These integer values correspond to the syslog "priority" levels and should not be changed
		Emergency = 0,
		Alert = 1,
		Critical = 2,
		Error = 3,
		Warning = 4,
		Notice = 5,
		Info = 6,
		Debug = 7
	}
}

