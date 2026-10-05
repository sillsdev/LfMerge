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
				LfStringField field = LfStringField.FromString(text);
				if (field != null)
					newInstance.Add(wsstr, field);
			}
			return newInstance;
		}

		public static LfMultiText FromSingleStringMapping(string key, string value)
		{
			LfStringField field = LfStringField.FromString(value);
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
			LfStringField field = LfStringField.FromString(text);
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
					LfStringField field = LfStringField.FromString(valueStr);
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

		/// <summary>
		/// The value for the first writing system in wsSearchOrder that has a non-empty one, falling
		/// back to the first non-empty value in any writing system LCM knows, together with that
		/// writing system's handle. Keys are resolved to handles before they are compared, so a key
		/// LF spells differently from LCM's id for the writing system still matches it.
		///
		/// Where two keys name one writing system, the value is the one WriteToLcm would write: the
		/// key spelled as the config spells it, or else the later one. That is chosen before empty
		/// values are left out, so a value an LF user has cleared under the config's spelling is
		/// not replaced by the export's hidden, stale one.
		/// </summary>
		/// <param name="configuredTags">
		/// Every writing-system spelling the project's config uses; see WriteToLcm.
		/// </param>
		/// <returns>The handle and the value, or (0, null) when no non-empty value resolves.</returns>
		public KeyValuePair<int, string> BestStringAndWsId(IEnumerable<int> wsSearchOrder, ILgWritingSystemFactory wsManager,
			ISet<string> configuredTags = null)
		{
			// Each writing system's key, in the order the writing systems first appear
			var keyFor = new Dictionary<int, string>();
			var order = new List<int>();
			foreach (string key in Keys)
			{
				int wsId = LanguageTags.WsIdFromLfTag(wsManager, key);
				if (wsId == 0)
					continue;
				string other;
				if (keyFor.TryGetValue(wsId, out other))
					keyFor[wsId] = PreferredKey(other, key, configuredTags);
				else
				{
					keyFor[wsId] = key;
					order.Add(wsId);
				}
			}
			var resolved = new List<KeyValuePair<int, string>>();
			foreach (int wsId in order)
			{
				LfStringField field = this[keyFor[wsId]];
				if (field != null && !field.IsEmpty)
					resolved.Add(new KeyValuePair<int, string>(wsId, field.Value));
			}
			foreach (int wsId in wsSearchOrder)
			{
				foreach (KeyValuePair<int, string> candidate in resolved)
				{
					if (candidate.Key == wsId)
						return candidate;
				}
			}
			return resolved.FirstOrDefault();
		}

		public KeyValuePair<string, string> FirstNonEmptyKeyValue()
		{
			KeyValuePair<string, LfStringField> result = this.FirstOrDefault(kv => kv.Value != null && !String.IsNullOrEmpty(kv.Value.Value));
			return (result.Value == null) ?
				new KeyValuePair<string, string>(null, null) :
				new KeyValuePair<string, string>(result.Key, result.Value.Value);
		}

		public bool WriteToLcmMultiString(IMultiAccessorBase dest, ILgWritingSystemFactory wsManager,
			ISet<string> configuredTags = null, Action<string> onUnidentifiedTag = null,
			Action<string, string> onKeyNotWritten = null)
		{
			if (dest == null)
				return false;
			return WriteToLcm(dest.AvailableWritingSystemIds, dest.get_String, dest.set_String, wsManager,
				configuredTags, onUnidentifiedTag, onKeyNotWritten);
		}

		/// <summary>
		/// Writes every alternative into an LCM multistring, then clears each alternative LCM holds
		/// that has no key here. The built-in multitext fields and the MultiUnicode custom fields
		/// both come through here, reaching LCM through an IMultiAccessorBase and through
		/// ISilDataAccess respectively, which is why LCM is reached through delegates.
		///
		/// Clearing is right because the LCM-to-Mongo direction exports every alternative LCM holds:
		/// one missing from LF is one a user removed there, not one LF never saw.
		///
		/// Each key is resolved with LanguageTags.WsIdFromLfTag. Resolving it any other way would
		/// skip a non-canonical key as unidentified, and the clearing pass would then blank that
		/// writing system's text in LCM: the data would not merely be dropped but deleted.
		///
		/// An alternative whose value has not changed is left alone, so it stays out of the .fwdata
		/// XML and out of the Mercurial commit.
		///
		/// Two keys can name the same writing system, spelled two ways that both resolve to it, and
		/// only one can be written. That happens when LF's config spells a writing system otherwise
		/// than LfMerge's export: the export keys every value by the writing system's Id, an LF user
		/// edits under the config's spelling, and the export's value stays in the entry, hidden
		/// from the editor, until the entry is next exported -- which, the edit once written to LCM,
		/// may be a long time. In spt-flex, whose config says "hi-IN" for the Id "hi-Deva-IN", an
		/// edited entry holds both. The value to write is the one under the spelling the config
		/// uses, since that is what LF's editor shows and so the one a user can have edited; the
		/// other is the hidden export, or, after the config has been re-spelled, an edit made under
		/// the old spelling that has already been written. Where both spellings or neither are in
		/// the config, it is the later key, as it always was: LF adds a new key after the existing
		/// ones.
		/// </summary>
		/// <param name="existingWsIds">The writing systems LCM currently holds alternatives for.</param>
		/// <param name="getAlternative">Reads LCM's alternative for a writing system.</param>
		/// <param name="setAlternative">Writes LCM's alternative for a writing system.</param>
		/// <param name="wsManager">Resolves LF's keys to LCM writing systems.</param>
		/// <param name="configuredTags">
		/// Every writing-system spelling the project's config uses, to choose between two keys
		/// naming one writing system; null to go on key order alone.
		/// </param>
		/// <param name="onUnidentifiedTag">Told each key that resolves to no writing system.</param>
		/// <param name="onKeyNotWritten">
		/// Told each key left unwritten because another key names the same writing system, and the
		/// key written instead.
		/// </param>
		/// <returns>Whether anything in LCM changed.</returns>
		public bool WriteToLcm(IEnumerable<int> existingWsIds, Func<int, ITsString> getAlternative,
			Action<int, ITsString> setAlternative, ILgWritingSystemFactory wsManager,
			ISet<string> configuredTags = null, Action<string> onUnidentifiedTag = null,
			Action<string, string> onKeyNotWritten = null)
		{
			// Each writing system's key, chosen before anything is written
			var keyFor = new Dictionary<int, string>();
			foreach (string key in Keys)
			{
				int wsId = LanguageTags.WsIdFromLfTag(wsManager, key);
				if (wsId == 0)
				{
					onUnidentifiedTag?.Invoke(key);
					continue;
				}
				string other;
				if (keyFor.TryGetValue(wsId, out other))
				{
					string kept = PreferredKey(other, key, configuredTags);
					onKeyNotWritten?.Invoke(kept == key ? other : key, kept);
					keyFor[wsId] = kept;
				}
				else
					keyFor[wsId] = key;
			}

			var wsIdsToClear = new HashSet<int>(existingWsIds);
			bool changed = false;
			foreach (KeyValuePair<int, string> chosen in keyFor)
			{
				int wsId = chosen.Key;
				LfStringField field = this[chosen.Value];
				wsIdsToClear.Remove(wsId);
				string text = (field == null) ? string.Empty : (field.Value ?? string.Empty);
				ITsString newValue = LfMerge.Core.DataConverters.ConvertMongoToLcmTsStrings.SpanStrToTsString(text, wsId, wsManager);
				ITsString oldValue = getAlternative(wsId);
				// GetDiffsInTsStrings() returns null when there are no changes
				if (oldValue != null && TsStringUtils.GetDiffsInTsStrings(oldValue, newValue) == null)
					continue;
				setAlternative(wsId, newValue);
				changed = true;
			}
			foreach (int wsId in wsIdsToClear)
			{
				ITsString oldValue = getAlternative(wsId);
				if (oldValue == null || string.IsNullOrEmpty(oldValue.Text))
					continue;
				setAlternative(wsId, TsStringUtils.EmptyString(wsId));
				changed = true;
			}
			return changed;
		}

		/// <summary>The start of one key's text, enough to find the entry it is in.</summary>
		public string Excerpt(string key)
		{
			LfStringField field;
			string text = (TryGetValue(key, out field) && field != null) ? (field.Value ?? string.Empty) : string.Empty;
			return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
		}

		/// <summary>
		/// Of two keys naming one writing system, the one to write: the one spelled as the config
		/// spells it, or else the later one. LF's editor matches spellings exactly, so this does too.
		/// </summary>
		private static string PreferredKey(string earlier, string later, ISet<string> configuredTags)
		{
			if (configuredTags != null)
			{
				bool earlierConfigured = configuredTags.Contains(earlier);
				if (earlierConfigured != configuredTags.Contains(later))
					return earlierConfigured ? earlier : later;
			}
			return later;
		}

	}
}

