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
	public class ConvertFdoToMongoTsStrings
	{
		private int[] _wsSearchOrder;

		public ConvertFdoToMongoTsStrings(IEnumerable<int> wsPreferences)
		{
			_wsSearchOrder = wsPreferences.ToArray();
		}

		public ConvertFdoToMongoTsStrings(IEnumerable<ILgWritingSystem> wsPreferences)
		{
			_wsSearchOrder = wsPreferences.Select(ws => ws.Handle).ToArray();
		}

		public ConvertFdoToMongoTsStrings(IEnumerable<string> wsPreferences, ILgWritingSystemFactory wsf)
		{
			_wsSearchOrder = wsPreferences.Select(wsName => wsf.GetWsFromStr(wsName)).ToArray();
		}

		public static string SafeTsStringText(ITsString tss)
		{
			if (tss == null)
				return null;
			return tss.Text;
		}

		public string BestString(IMultiAccessorBase multiString)
		{
			// If this is an IMultiStringAccessor, we can just hand it off to GetBestAlternative
			var accessor = multiString as IMultiStringAccessor;
			if (accessor != null)
			{
				int wsActual;
				return SafeTsStringText(accessor.GetBestAlternative(out wsActual, _wsSearchOrder));
			}
			// JUst a MultiAccessorBase? Then search manually
			string result;
			foreach (int wsId in _wsSearchOrder)
			{
				result = SafeTsStringText(multiString.StringOrNull(wsId));
				if (result != null)
					return result;
			}
			return null;
		}
	}
}

