// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge
{
	public interface ILanguageDepotProject
	{
		void Initialize(string lfProjectCode);

		string Username { get; }

		string Password { get; }

		string Identifier { get; }

		string Repository { get; }
	}
}

