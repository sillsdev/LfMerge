// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.LanguageForge.Model;
using LfMergeBridge.LfMergeModel;
using LfMerge.Core.Reporting;
using MongoDB.Bson;
using SIL.Progress;
using LfMergeBridge;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToLcmComments
	{
		private IMongoConnection _conn;
		private ILfProject _project;
		private ConversionError<LfLexEntry> _entryConversionErrors;
		private ILogger _logger;
		private IProgress _progress;

		public ConvertMongoToLcmComments(IMongoConnection conn, ILfProject proj, ConversionError<LfLexEntry> entryConversionErrors, ILogger logger, IProgress progress)
		{
			_conn = conn;
			_project = proj;
			_entryConversionErrors = entryConversionErrors;
			_logger = logger;
			_progress = progress;
		}

		public List<CommentConversionError<LfLexEntry>> RunConversion(Dictionary<MongoDB.Bson.ObjectId, Guid> entryObjectIdToGuidMappings)
		{
			var skippedEntries = _entryConversionErrors.EntryErrors.ToDictionary(s => s.EntryGuid());
			var exceptions = new List<CommentConversionError<LfLexEntry>>();
			var commentsWithIds = new List<KeyValuePair<string, LfComment>>();
			foreach (var comment in _conn.GetComments(_project))
			{
				try
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
					// Skip comment if, and only if, the entry was skipped
					if (Guid.TryParse(comment.Regarding?.TargetGuid ?? "", out Guid targetGuid)) {
						if (skippedEntries.TryGetValue(targetGuid, out var entryConversionError)) {
							exceptions.Add(new CommentConversionError<LfLexEntry>(comment, null, entryConversionError));
						}
					}

					commentsWithIds.Add(new KeyValuePair<string, LfComment>(comment.Id.ToString(), comment));
				}
				catch (Exception e)
				{
					// Try to look up the target GUID if possible
					if (Guid.TryParse(comment.Regarding?.TargetGuid ?? "", out Guid targetGuid)) {
						if (skippedEntries.TryGetValue(targetGuid, out var entryConversionError)) {
							exceptions.Add(new CommentConversionError<LfLexEntry>(comment, e, entryConversionError));
						} else {
							exceptions.Add(new CommentConversionError<LfLexEntry>(comment, e, null, targetGuid));
						}
					} else {
						exceptions.Add(new CommentConversionError<LfLexEntry>(comment, e, null));
					}
				}
			}
			var skippedCommentGuids = new HashSet<string>(exceptions.Select(s => s.CommentGuid().ToString()));
			var unskippedComments = commentsWithIds.Where(s => !skippedCommentGuids.Contains(s.Key)).ToList();
			string bridgeOutput;
			var response = CallLfMergeBridge(unskippedComments, out bridgeOutput);
			if (response != null)
			{
				// LfMergeBridge returns two lists of IDs (comment IDs or reply IDs) that need to have their GUIDs updated in Mongo.
				_conn.SetCommentGuids(_project, response.CommentIdsThatNeedGuids);
				_conn.SetCommentReplyGuids(_project, response.ReplyIdsThatNeedGuids);
			}
			return exceptions;
		}

		private LfLexEntry GetLexEntry(ObjectId idOfEntry)
		{
			return _conn.GetRecords<LfLexEntry>(_project, MagicStrings.LfCollectionNameForLexicon, entry => entry.Id == idOfEntry).First();
		}

		private WriteToChorusNotesResponse CallLfMergeBridge(List<KeyValuePair<string, LfComment>> lfComments, out string bridgeOutput)
		{
			return CallLfMergeBridge(lfComments, out bridgeOutput, _project.FwDataPath, _progress, _logger);
		}

		public static WriteToChorusNotesResponse CallLfMergeBridge(List<KeyValuePair<string, LfComment>> lfComments, out string bridgeOutput, string fwDataPath, IProgress progress, ILogger logger)
		{
			bridgeOutput = string.Empty;
			var options = new Dictionary<string, string>
			{
				{"-p", fwDataPath},
			};
			try {
				var bridgeInput = new WriteToChorusNotesInput { LfComments = lfComments };
				LfMergeBridge.LfMergeBridge.ExtraInputData.Add(options, bridgeInput);
				if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Write_To_Chorus_Notes", progress,
					options, out bridgeOutput))
				{
					logger.Error("Got an error from Language_Forge_Write_To_Chorus_Notes: {0}", bridgeOutput);
					return null;
				}
				else
				{
					var success = LfMergeBridge.LfMergeBridge.ExtraOutputData.TryGetValue(options, out var outputObject);
					if (success)
					{
						return outputObject as WriteToChorusNotesResponse;
					}
					else
					{
						logger.Error("Language_Forge_Write_To_Chorus_Notes failed to return any data. Its output was: {0}", bridgeOutput);
						return null;
					}
				}
			}
			catch (NullReferenceException)
			{
				logger.Debug("Got an exception. Before rethrowing it, here is what LfMergeBridge sent:");
				logger.Debug("{0}", bridgeOutput);
				throw;
			}
		}
	}
}
