// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfMultiText : Dictionary<string, LfStringField> // Note: NOT derived from LfFieldBase
	{
		public bool IsEmpty { get { return Count <= 0; } }

		public static LfMultiText FromFdoMultiString(IMultiAccessorBase other, WritingSystemManager wsManager)
		{
			LfMultiText newInstance = new LfMultiText();
			foreach (int wsid in other.AvailableWritingSystemIds)
			{
				string wsstr = wsManager.GetStrFromWs(wsid);
				ITsString value = other.get_String(wsid);
				newInstance.Add(wsstr, new LfStringField { Value = value.Text });
				// TODO: Deal with runs; using just ITsString.Text will discard any useful information found in Run elements, if present.
				// Example code follows:
				/*
				foreach (TsRunPart run in value.Runs())
				{
					if (run.Props.Style() == null)
						Console.WriteLine("No style");
					else
						Console.WriteLine("Run style {0}", run.Props.Style());
					Console.WriteLine("Run writing system {0}", wsManager.GetStrFromWs(run.Props.GetWs()));
					Console.WriteLine("Run text {0}", run.Text);
				}
				*/
			}
			return newInstance;
		}

		public static LfMultiText FromSingleStringMapping(string key, string value)
		{
			return new LfMultiText { { key, new LfStringField { Value = value } } };
		}

		public static LfMultiText FromSingleITsStringMapping(string key, ITsString value)
		{
			if (value == null || value.Text == null) return null;
			return new LfMultiText { { key, new LfStringField { Value = value.Text } } };
		}

		public static LfMultiText FromSingleITsString(ITsString value, WritingSystemManager wsm)
		{
			if (value == null || value.Text == null) return null;
			int wsId = value.get_WritingSystem(0);
			string wsStr = wsm.GetStrFromWs(wsId);
			return new LfMultiText { { wsStr, new LfStringField { Value = value.Text } } };
		}

		public Dictionary<string, string> AsStringDictionary()
		{
			return this.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
		}

		public BsonDocument AsBsonDocument()
		{
			BsonDocument result = new BsonDocument();
			foreach (KeyValuePair<string, LfStringField> kv in this)
			{
				result.Add(kv.Key, new BsonDocument(kv.Value.AsDictionary()));
			}
			return result;
		}

		public string FirstNonEmptyString()
		{
			return AsStringDictionary().Values.FirstOrDefault(str => !String.IsNullOrEmpty(str)); // TODO: Use best analysis or vernacular instead of just first non-blank entry.
		}

		public LfStringField FirstNonEmptyStringField()
		{
			return Values.FirstOrDefault(strFld => !String.IsNullOrEmpty(strFld.Value)); // TODO: Use best analysis or vernacular instead of just first non-blank entry.
		}

		public KeyValuePair<int, string> WsIdAndFirstNonEmptyString(FdoCache cache)
		{
			KeyValuePair<string, string> kv = FirstNonEmptyKeyValue();
			if (kv.Key == null) return new KeyValuePair<int, string>();
			WritingSystemManager wsm = cache.ServiceLocator.WritingSystemManager;
			int wsId = wsm.GetWsFromStr(kv.Key);
			return new KeyValuePair<int, string>(wsId, kv.Value);
		}

		public KeyValuePair<string, string> FirstNonEmptyKeyValue()
		{
			return AsStringDictionary().FirstOrDefault(kv => !String.IsNullOrEmpty(kv.Value));
		}

		// TODO: If we need to pass in an FdoCache, this method probably doesn't belong on LfMultiText...
		public ITsString ToITsString(int wsId, FdoCache cache)
		{
			WritingSystemManager wsm = cache.ServiceLocator.WritingSystemManager;
			string wsStr = wsm.GetStrFromWs(wsId);
			LfStringField valueField;
			if (TryGetValue(wsStr, out valueField))
				return TsStringUtils.MakeTss(valueField.Value, wsId);
			else
				return TsStringUtils.MakeTss(FirstNonEmptyString(), wsId);
		}

		public ITsString ToAnalysisITsString(FdoCache cache)
		{
			return ToITsString(cache.DefaultAnalWs, cache);
		}

		public void WriteToFdoMultiString(IMultiAccessorBase dest, WritingSystemManager wsManager)
		{
			/*
			LfMultiText newInstance = new LfMultiText();
			foreach (int wsid in dest.AvailableWritingSystemIds)
			{
				string wsstr = wsManager.GetStrFromWs(wsid);
				ITsString value = dest.get_String(wsid);
				newInstance.Add(wsstr, new LfStringField { Value = value.Text });
			}
			return newInstance;
			*/
			foreach (KeyValuePair<string, string> kv in this.AsStringDictionary())
			{
				int wsId = wsManager.GetWsFromStr(kv.Key);
				if (wsId == 0)
				{
					// Skip any unidentified writing systems
					continue;
				}
				string value = kv.Value;
				dest.set_String(wsId, value);
			}
		}

	}
}

