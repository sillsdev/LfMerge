// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.DataConverters
{
	public class ConvertWritingSystems
	{
		private BidirectionalDictionary<int, string> _wssAll = new BidirectionalDictionary<int, string>();

		private BidirectionalDictionary<int, string> _wssAnalysis      = new BidirectionalDictionary<int, string>();
		private BidirectionalDictionary<int, string> _wssPronunciation = new BidirectionalDictionary<int, string>();
		private BidirectionalDictionary<int, string> _wssVernacular    = new BidirectionalDictionary<int, string>();

		public IEnumerable<KeyValuePair<int, string>> AnalysisWritingSystems      { get { return _wssAnalysis; } }
		public IEnumerable<KeyValuePair<int, string>> PronunciationWritingSystems { get { return _wssPronunciation; } }
		public IEnumerable<KeyValuePair<int, string>> VernacularWritingSystems    { get { return _wssVernacular; } }

		public IEnumerable<string> AnalysisWritingSystemNames      { get { return AnalysisWritingSystems.Select(kv => kv.Value); } }
		public IEnumerable<string> PronunciationWritingSystemNames { get { return PronunciationWritingSystems.Select(kv => kv.Value); } }
		public IEnumerable<string> VernacularWritingSystemNames    { get { return VernacularWritingSystems.Select(kv => kv.Value); } }

		public IEnumerable<int> AnalysisWritingSystemIds      { get { return AnalysisWritingSystems.Select(kv => kv.Key); } }
		public IEnumerable<int> PronunciationWritingSystemIds { get { return PronunciationWritingSystems.Select(kv => kv.Key); } }
		public IEnumerable<int> VernacularWritingSystemIds    { get { return VernacularWritingSystems.Select(kv => kv.Key); } }

		public ConvertWritingSystems(FdoCache cache)
		{
			var langProj = cache.LanguageProject;
			foreach (var ws in langProj.AllWritingSystems)
				_wssAll.Add(ws.Handle, ws.Id);
			foreach (var ws in langProj.CurrentAnalysisWritingSystems)
				_wssAnalysis.Add(ws.Handle, ws.Id);
			foreach (var ws in langProj.CurrentPronunciationWritingSystems)
				_wssPronunciation.Add(ws.Handle, ws.Id);
			foreach (var ws in langProj.CurrentVernacularWritingSystems)
				_wssVernacular.Add(ws.Handle, ws.Id);
		}

		public int GetWsFromStr(string wsStr)
		{
			int wsId;
			if (_wssAll.TryGetValueBySecond(wsStr, out wsId))
				return wsId;
			return 0;
		}

		public string GetStrFromWs(int wsId)
		{
			string wsStr;
			if (_wssAll.TryGetValueByFirst(wsId, out wsStr))
				return wsStr;
			return "";
		}
	}
}

