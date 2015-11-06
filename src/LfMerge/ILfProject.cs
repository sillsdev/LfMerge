// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
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
		LanguageDepotProject LanguageDepotProject { get; }
	}
}

