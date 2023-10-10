// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfMultiText : Dictionary<string, LfStringField> // Note: NOT derived from LfFieldBase
	{
		public bool IsEmpty { get { return (Count <= 0) || (this.All(kv => kv.Value == null || kv.Value.IsEmpty)); } }

		public static LfMultiText FromLcmMultiString(IMultiAccessorBase other, ILgWritingSystemFactory wsManager)
		{
			LfMultiText newInstance = new LfMultiText();
			foreach (int wsid in other.AvailableWritingSystemIds)
			{
				string wsstr = wsManager.GetStrFromWs(wsid);
				ITsString value = other.get_String(wsid);
				string text = LfMerge.Core.DataConverters.ConvertLcmToMongoTsStrings.TextFromTsString(value, wsManager);
				LfStringField field = LfStringField.CreateFrom(text);
				if (field != null)
					newInstance.Add(wsstr, field);
			}
			return newInstance;
		}

		public static LfMultiText FromSingleStringMapping(string key, string value)
		{
			LfStringField field = LfStringField.CreateFrom(value);
			if (field == null)
				return null;
			return new LfMultiText { { key, field } };
		}

		public static LfMultiText FromSingleITsString(ITsString value, ILgWritingSystemFactory wsManager)
		{
			if (value == null || value.Text == null) return null;
			int wsId = value.get_WritingSystem(0);
			string wsStr = wsManager.GetStrFromWs(wsId);
			string text = LfMerge.Core.DataConverters.ConvertLcmToMongoTsStrings.TextFromTsString(value, wsManager);
			LfStringField field = LfStringField.CreateFrom(text);
			if (field == null)
				return null;
			return new LfMultiText { { wsStr, field } };
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
					string valueStr = LfMerge.Core.DataConverters.ConvertLcmToMongoTsStrings.TextFromTsString(tss, wsManager);
					LfStringField field = LfStringField.CreateFrom(valueStr);
					if (field != null)
						mt.Add(wsStr, field);
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

		public KeyValuePair<int, string> WsIdAndFirstNonEmptyString(LcmCache cache)
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

		public void WriteToLcmMultiString(IMultiAccessorBase dest, ILgWritingSystemFactory wsManager)
		{
			if (dest == null)
				return;
			HashSet<int> destWsIdsToClear = new HashSet<int>(dest.AvailableWritingSystemIds);
			foreach (KeyValuePair<string, LfStringField> kv in this)
			{
				int wsId = wsManager.GetWsFromStr(kv.Key);
				if (wsId == 0) continue; // Skip any unidentified writing systems
				string value = kv.Value.Value;
				ITsString tss = LfMerge.Core.DataConverters.ConvertMongoToLcmTsStrings.SpanStrToTsString(value, wsId, wsManager);
				dest.set_String(wsId, tss);
				destWsIdsToClear.Remove(wsId);
			}
			foreach (int wsId in destWsIdsToClear)
			{
				dest.set_String(wsId, string.Empty);
			}
		}

	}
}

