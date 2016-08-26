// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;

namespace LfMerge.Core.LanguageForge.Infrastructure
{
	public interface ILanguageForgeProxy
	{
		string UpdateCustomFieldViews(string projectCode, List<CustomFieldSpec> customFieldSpecs);
		string UpdateCustomFieldViews(string projectCode, List<CustomFieldSpec> customFieldSpecs, bool isTest);

		string ListUsers();
	}
}

