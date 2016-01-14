// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using LfMerge.LanguageForge.Config;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfLexicon
	{
		private ILfProjectConfig Config { get; set; }

		public LfLexicon(ILfProjectConfig config)
		{
			Config = config;
		}
	}
}

