﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge
{
	public interface IProcessingStateDeserialize
	{
		ProcessingState Deserialize(string projectCode);
	}
}
