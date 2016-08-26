// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core
{
	public interface ILanguageDepotProject
	{
		void Initialize(string lfProjectCode);

		string Identifier { get; }

		string Repository { get; }
	}
}

