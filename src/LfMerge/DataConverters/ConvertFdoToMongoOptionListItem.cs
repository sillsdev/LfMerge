// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoOptionListItem
	{
		private LfOptionList _lfOptionList;
		private Dictionary<Guid, string> _lfOptionListItemKeyByGuid;
		private ILogger _logger;

		public ConvertFdoToMongoOptionListItem(LfOptionList lfOptionList, ILogger logger)
		{
			_logger = logger;
			_lfOptionList = lfOptionList;
			if (_lfOptionList != null)
			{
				_lfOptionListItemKeyByGuid = _lfOptionList.Items.ToDictionary(
					item => item.Guid.GetValueOrDefault(),
					item => item.Key
				);
			}
		}

		public string LfKeyString(ICmPossibility fdoOptionListItem, int ws)
		{
			string result;
			if (fdoOptionListItem == null)
				return null;
			
			if (_lfOptionList != null)
			{
				if (_lfOptionListItemKeyByGuid.TryGetValue(fdoOptionListItem.Guid, out result))
					return result;
				
				// We shouldn't get here, because the option list SHOULD be pre-populated.
				_logger.Error("Got an option list item without a corresponding LF option list item. " +
					"In option list name '{0}', list code '{1}': " +
					"FDO option list item '{2}' had GUID {3} but no LF option list item was found",
					_lfOptionList.Name, _lfOptionList.Code,
					fdoOptionListItem.AbbrAndName, fdoOptionListItem.Guid
				);
				return null;
			}

			if (fdoOptionListItem.Abbreviation == null || fdoOptionListItem.Abbreviation.get_String(ws) == null)
			{
				// Last-ditch effort
				char ORC = '\ufffc';
				return fdoOptionListItem.AbbrevHierarchyString.Split(ORC).LastOrDefault();
			}
			else
			{
				return ConvertFdoToMongoTsStrings.SafeTsStringText(fdoOptionListItem.Abbreviation.get_String(ws));
			}
		}
	}
}
