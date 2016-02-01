// Copyright (c) 2016 SIL International
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

		public BsonDocument AsBsonDocument()
		{
			BsonDocument result = new BsonDocument();
			foreach (KeyValuePair<string, LfStringField> kv in this)
			{
				result.Add(kv.Key, new BsonDocument(kv.Value.AsDictionary()));
			}
			return result;
		}

		public KeyValuePair<string, string> BestStringAndWs(IEnumerable<string> wsSearchOrder)
		{
			foreach (string ws in wsSearchOrder)
			{
				LfStringField field;
				if (this.TryGetValue(ws, out field) && field != null && !String.IsNullOrEmpty(field.Value))
					return new KeyValuePair<string, string>(ws, field.Value);
			}
			// Fall back to first non-empty string
			return FirstNonEmptyKeyValue();
		}

		public string BestString(IEnumerable<string> wsSearchOrder)
		{
			return BestStringAndWs(wsSearchOrder).Value;
		}

		public LfStringField FirstNonEmptyStringField()
		{
			// TODO: Most functions that call this should instead call BestStringAndWs() and pass in a search list
			return Values.FirstOrDefault(field => field != null && !String.IsNullOrEmpty(field.Value));
		}

		public string FirstNonEmptyString()
		{
			// TODO: Most functions that call this should instead call BestString() and pass in a search list.
			LfStringField result = FirstNonEmptyStringField();
			return (result == null) ? null : result.Value;
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
			KeyValuePair<string, LfStringField> result = this.FirstOrDefault(kv => kv.Value != null && !String.IsNullOrEmpty(kv.Value.Value));
			return (result.Value == null) ?
				new KeyValuePair<string, string>(null, null) :
				new KeyValuePair<string, string>(result.Key, result.Value.Value);
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
			foreach (KeyValuePair<string, LfStringField> kv in this)
			{
				int wsId = wsManager.GetWsFromStr(kv.Key);
				if (wsId == 0) continue; // Skip any unidentified writing systems
				string value = kv.Value.Value;
				dest.set_String(wsId, value);
			}
		}

	}
}

