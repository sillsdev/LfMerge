// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge
{
	public interface ILanguageDepotProject
	{
		void Initialize(string lfProjectCode);

		string Username { get; }

		string Password { get; }

		string ProjectCode { get; }
	}
}

