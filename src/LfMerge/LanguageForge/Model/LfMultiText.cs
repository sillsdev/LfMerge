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

		public static LfMultiText FromFdoMultiString(IMultiAccessorBase other, ILgWritingSystemFactory wsManager)
		{
			LfMultiText newInstance = new LfMultiText();
			foreach (int wsid in other.AvailableWritingSystemIds)
			{
				string wsstr = wsManager.GetStrFromWs(wsid);
				ITsString value = other.get_String(wsid);
				string text = LfMerge.DataConverters.ConvertFdoToMongoTsStrings.TextFromTsString(value, wsManager);
				newInstance.Add(wsstr, LfStringField.FromString(text));
			}
			return newInstance;
		}

		public static LfMultiText FromSingleStringMapping(string key, string value)
		{
			return new LfMultiText { { key, LfStringField.FromString(value) } };
		}

		public static LfMultiText FromSingleITsString(ITsString value, ILgWritingSystemFactory wsManager)
		{
			if (value == null || value.Text == null) return null;
			int wsId = value.get_WritingSystem(0);
			string wsStr = wsManager.GetStrFromWs(wsId);
			string text = LfMerge.DataConverters.ConvertFdoToMongoTsStrings.TextFromTsString(value, wsManager);
			return new LfMultiText { { wsStr, LfStringField.FromString(text) } };
		}

		public static LfMultiText FromMultiITsString(ITsMultiString value, ILgWritingSystemFactory wsManager)
		{
			if (value == null || value.StringCount == 0) return null;
			LfMultiText mt = new LfMultiText();
			for (int index = 0; index < value.StringCount; index++)
			{
				int wsId;
				ITsString tss = value.GetStringFromIndex(index, out wsId);
				string wsStr = wsManager.GetStrFromWs(wsId);
				if (!string.IsNullOrEmpty(wsStr))
				{
					string valueStr = LfMerge.DataConverters.ConvertFdoToMongoTsStrings.TextFromTsString(tss, wsManager);
					mt.Add(wsStr, LfStringField.FromString(valueStr));
				}
				//MainClass.Logger.Warning("Adding multistring ws: {0}, str {1}", wsStr, valueStr);
			}
			return mt;
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
				if (ws == null) // Shouldn't happen, but apparently does happen sometimes. TODO: Find out why.
					continue;
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
			ILgWritingSystemFactory wsManager = cache.ServiceLocator.WritingSystemManager;
			int wsId = wsManager.GetWsFromStr(kv.Key);
			return new KeyValuePair<int, string>(wsId, kv.Value);
		}

		public KeyValuePair<string, string> FirstNonEmptyKeyValue()
		{
			KeyValuePair<string, LfStringField> result = this.FirstOrDefault(kv => kv.Value != null && !String.IsNullOrEmpty(kv.Value.Value));
			return (result.Value == null) ?
				new KeyValuePair<string, string>(null, null) :
				new KeyValuePair<string, string>(result.Key, result.Value.Value);
		}

		public void WriteToFdoMultiString(IMultiAccessorBase dest, ILgWritingSystemFactory wsManager)
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

