// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Queues
{
	public interface IQueue
	{
		QueueNames Name { get; }

		bool IsEmpty { get; }

		string[] QueuedProjects { get; }
	}
}

