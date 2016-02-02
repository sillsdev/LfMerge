// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.CoreImpl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LfMerge.DataConverters
{
	public class TsStringConverter
	{
		private FdoCache _cache;
		private int _wsEn;

		public TsStringConverter(FdoCache cache, IEnumerable<int> wsPreferences)
		{
			// Or should wsPreferences be a list of strings? TODO: Consider it.
			_cache = cache;
			_wsEn = cache.WritingSystemFactory.GetWsFromStr("en");
		}

		public string SafeTsStringText(ITsString tss)
		{
			if (tss == null)
				return null;
			return tss.Text;
		}

		public string AnalysisText(IMultiAccessorBase multiString)
		{
			return SafeTsStringText(multiString.get_String(_cache.DefaultAnalWs));
		}

		public string EnglishText(IMultiAccessorBase multiString)
		{
			return SafeTsStringText(multiString.get_String(_wsEn));
		}

		public string PronunciationText(IMultiAccessorBase multiString)
		{
			return SafeTsStringText(multiString.get_String(_cache.DefaultPronunciationWs));
		}

		public string UserText(IMultiAccessorBase multiString)
		{
			return SafeTsStringText(multiString.get_String(_cache.DefaultUserWs));
		}

		public string VernacularText(IMultiAccessorBase multiString)
		{
			return SafeTsStringText(multiString.get_String(_cache.DefaultVernWs));
		}

		public string BestString(IMultiAccessorBase multiString, IEnumerable<int> wsPreferenceOrder)
		{
			string result;
			foreach (int wsId in wsPreferenceOrder)
			{
				result = SafeTsStringText(multiString.StringOrNull(wsId));
				if (result != null)
					return result;
			}
			return null;
		}

		public string BestString(IMultiAccessorBase multiString, IEnumerable<string> wsPreferenceOrder)
		{
			IEnumerable<int> wsPreferenceIds = wsPreferenceOrder
				.Select(wsStr => _cache.WritingSystemFactory.GetWsFromStr(wsStr));
			return BestString(multiString, wsPreferenceIds);
		}
	}
}

