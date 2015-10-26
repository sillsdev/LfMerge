// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Actions
{
	public class UpdateMongoDbFromFdo: Action
	{
		public override void Run(ILfProject project)
		{
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

