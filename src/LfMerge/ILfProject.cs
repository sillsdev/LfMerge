// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.FieldWorks;

namespace LfMerge
{
	public interface ILfProject
	{
		string LfProjectCode { get; }
		string FwProjectCode { get; }
		string MongoDatabaseName { get; }
		FwProject FieldWorksProject { get; }
		ProcessingState State { get; }
		ILanguageDepotProject LanguageDepotProject { get; }
		string LanguageDepotProjectUri { get; }
	}
}

