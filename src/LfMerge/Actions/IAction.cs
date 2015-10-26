// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Actions
{
	public interface IAction
	{
		ActionNames Name { get; }

		IAction NextAction { get; }

		void Run(ILfProject project);
	}
}

