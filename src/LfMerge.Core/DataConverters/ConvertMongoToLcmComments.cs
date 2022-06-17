// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.LanguageForge.Model;
using MongoDB.Bson;
using Newtonsoft.Json;
using SIL.Progress;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToLcmComments
	{
		private IMongoConnection _conn;
		private ILfProject _project;
		private List<Tuple<LfLexEntry, Exception>> _entryConversionErrors;
		private ILogger _logger;
		private IProgress _progress;

		public ConvertMongoToLcmComments(IMongoConnection conn, ILfProject proj, List<Tuple<LfLexEntry, Exception>> entryConversionErrors, ILogger logger, IProgress progress)
		{
			_conn = conn;
			_project = proj;
			_entryConversionErrors = entryConversionErrors;
			_logger = logger;
			_progress = progress;
		}

		public void RunConversion(Dictionary<MongoDB.Bson.ObjectId, Guid> entryObjectIdToGuidMappings)
		{
			var commentsWithIds = new List<KeyValuePair<string, LfComment>>();
			foreach (var comment in _conn.GetComments(_project))
			{
				Guid guid;
				// LfMergeBridge wants lex entry GUIDs (passed along in comment.Regarding.TargetGuid), not Mongo ObjectIds like comment.EntryRef contains.
				if (comment.EntryRef != null &&
					entryObjectIdToGuidMappings.TryGetValue(comment.EntryRef, out guid))
				{
					comment.Regarding.TargetGuid = guid.ToString();

					// LF-186
					if (string.IsNullOrEmpty(comment.Regarding.Word))
					{
						var lexeme = GetLexEntry(comment.EntryRef).Lexeme.FirstNonEmptyString();
						var field = comment.Regarding.FieldNameForDisplay;
						var ws = comment.Regarding.InputSystemAbbreviation;
						var value = string.IsNullOrEmpty(comment.Regarding.FieldValue)
							? ""
							: string.Format(" \"{0}\"", comment.Regarding.FieldValue);
						comment.Regarding.Word = string.Format("{0} ({1} - {2}{3})", lexeme,
							field, ws, value);
				}
				}

				commentsWithIds.Add(new KeyValuePair<string, LfComment>(comment.Id.ToString(), comment));
			}
			string allCommentsJson = JsonConvert.SerializeObject(commentsWithIds);
			string bridgeOutput;
			CallLfMergeBridge(allCommentsJson, out bridgeOutput);
			// LfMergeBridge returns two lists of IDs (comment IDs or reply IDs) that need to have their GUIDs updated in Mongo.
			string commentGuidMappingsStr = GetPrefixedStringFromLfMergeBridgeOutput(bridgeOutput, "New comment ID->Guid mappings: ");
			string replyGuidMappingsStr = GetPrefixedStringFromLfMergeBridgeOutput(bridgeOutput, "New reply ID->Guid mappings: ");
			Dictionary<string, Guid> commentIdToGuidMappings = ParseGuidMappings(commentGuidMappingsStr);
			Dictionary<string, Guid> uniqIdToGuidMappings = ParseGuidMappings(replyGuidMappingsStr);
			_conn.SetCommentGuids(_project, commentIdToGuidMappings);
			_conn.SetCommentReplyGuids(_project, uniqIdToGuidMappings);
		}

		private LfLexEntry GetLexEntry(ObjectId idOfEntry)
		{
			return _conn.GetRecords<LfLexEntry>(_project, MagicStrings.LfCollectionNameForLexicon, entry => entry.Id == idOfEntry).First();
		}

		private bool CallLfMergeBridge(string bridgeInput, out string bridgeOutput)
		{
			bridgeOutput = string.Empty;
			using (var tmpFile = new SIL.IO.TempFile(bridgeInput))
			{
				var options = new Dictionary<string, string>
				{
					{"-p", _project.FwDataPath},
					{"serializedCommentsFromLfMerge", tmpFile.Path}
				};
				try {
				if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Write_To_Chorus_Notes", _progress,
					options, out bridgeOutput))
				{
					_logger.Error("Got an error from Language_Forge_Write_To_Chorus_Notes: {0}", bridgeOutput);
					return false;
				}
				else
				{
					// _logger.Debug("Good  output from Language_Forge_Write_To_Chorus_Notes: {0}", bridgeOutput);
					return true;
				}
				}
				catch (NullReferenceException)
				{
					_logger.Debug("Got an exception. Before rethrowing it, here is what LfMergeBridge sent:");
					_logger.Debug("{0}", bridgeOutput);
					throw;
				}
			}
		}

		public static string GetPrefixedStringFromLfMergeBridgeOutput(string lfMergeBridgeOutput, string prefix)
		{
			if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(lfMergeBridgeOutput))
			{
				return string.Empty;
			}
			string result = LfMergeBridgeServices.GetLineContaining(lfMergeBridgeOutput, prefix);
			if (result.StartsWith(prefix))
			{
				return result.Substring(prefix.Length);
			}
			else
			{
				return string.Empty; // If the "prefix" wasn't actually a prefix, this wasn't the string we wanted.
			}
		}

		public Dictionary<string, Guid> ParseGuidMappings(string input)
		{
			string[] parts = input.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			var result = new Dictionary<string, Guid>();
			foreach (string part in parts)
			{
				string[] kv = part.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
				if (kv.Length == 2)
				{
					Guid parsed;
					if (Guid.TryParse(kv[1], out parsed))
					{
						result[kv[0]] = parsed;
					}
				}
			}
			return result;
		}
	}
}
