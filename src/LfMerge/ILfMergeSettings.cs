// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using SIL.FieldWorks.FDO;
using LfMerge.Queues;

namespace LfMerge
{
	public interface ILfMergeSettings : IFdoDirectories
	{
		// Properties defined in IFdoDirectories:
		// string DefaultProjectsDirectory { get; }
		// string ProjectsDirectory { get; }
		// string TemplateDirectory { get; }

		string MongoDbHostNameAndPort { get; }
		string StateDirectory { get; }
		string WebWorkDirectory { get; }
		string GetStateFileName(string projectCode);
		string GetQueueDirectory(QueueNames queue);
	}
}

