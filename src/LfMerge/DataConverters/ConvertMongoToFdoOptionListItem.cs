// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoOptionListItem
	{
		public ConvertMongoToFdoOptionListItem()
		{
		}

		// We ask for the writing system ID for English because the calling code already has that
		// TODO: Consider refactoring to where this is an instance method, and it looks up the
		// writing system for English only once. Measure whether that saves significant time or not.
		public static string ToLfStringKey(ICmPossibility lfOptionListItem, LfOptionList lfOptionList, int wsEn)
		{
			string result;
			if (lfOptionListItem == null)
				return null;
			if (lfOptionList != null)
			{
				// TODO: Optimize this by keeping lfOptionList in the object instance, and building the dictionary
				// only once.
				Dictionary<Guid, string> lfOptionListKeyByGuid = lfOptionList.Items.ToDictionary(
					item => item.Guid.GetValueOrDefault(),
					item => item.Key
				);
				if (lfOptionListKeyByGuid.TryGetValue(lfOptionListItem.Guid, out result))
					return result;
				// We shouldn't get here, because the grammar list SHOULD be pre-populated.
				// TODO: Make this a log message. (Pass an ILogger instance into the constructor first).
				Console.WriteLine("ERROR: Got a part of speech without a corresponding LF grammar entry. " +
					"FDO PoS '{0}' had GUID {1} but no LF grammar entry was found",
					lfOptionListItem.AbbrAndName,
					lfOptionListItem.Guid
				);
				return null;
			}
			if (lfOptionListItem.Abbreviation == null || lfOptionListItem.Abbreviation.get_String(wsEn) == null)
			{
				// Last-ditch effort
				char ORC = '\ufffc';
				return lfOptionListItem.AbbrevHierarchyString.Split(ORC).LastOrDefault();
			}
			else
			{
				return ConvertFdoToMongoTsStrings.SafeTsStringText(lfOptionListItem.Abbreviation.get_String(wsEn));
			}
		}
	}
}

