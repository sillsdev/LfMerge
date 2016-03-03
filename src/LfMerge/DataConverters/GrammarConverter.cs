// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using LfMerge.LanguageForge.Model;

namespace LfMerge.DataConverters
{
	public class GrammarConverter : ConvertOptionList
	{
		public GrammarConverter(LfOptionList lfOptionList, int wsForKeys) : base(lfOptionList, wsForKeys, MagicStrings.LfOptionListCodeForGrammaticalInfo)
		{
		}

		public override LfOptionList PrepareOptionListUpdate(ICmPossibilityList fdoOptionList)
		{
			return base.PrepareOptionListUpdate(fdoOptionList);
		}

		private string AbbrevHierarchyStringForWs(ICmPossibility poss, int wsId)
		{
			// The CmPossibility.AbbrevHierarchyString property uses the default analysis language.
			// But we need to force a specific language (English) even if that is not the analysis language.
			string ORC = "\ufffc";
			ICmPossibility current = poss;
			LinkedList<ICmPossibility> allAncestors = new LinkedList<ICmPossibility>();
			while (current != null)
			{
				allAncestors.AddFirst(current);
				current = current.Owner as ICmPossibility;
			}
			// TODO: The below line might fail if one of them doesn't have a corresponding string. Deal with that case.
			return string.Join(ORC, allAncestors.Select(ancestor => ancestor.Abbreviation.get_String(wsId).Text));
		}

	}
}
