// Copyright (c) 2022 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.LanguageForge.Model;
using SIL.LCModel;
using LfMerge.Core.DataConverters;

namespace LfMerge.Core.Reporting
{
	public class ConversionError<TEntry>
	{
		public List<EntryConversionError<TEntry>> EntryErrors { get; set; }
		public List<CommentConversionError<TEntry>> CommentErrors { get; set; }

		public ConversionError() {
			EntryErrors = new List<EntryConversionError<TEntry>>();
			CommentErrors = new List<CommentConversionError<TEntry>>();
		}

		public bool Any() {
			return EntryErrors.Any() || CommentErrors.Any();
		}

		public int EntryErrorCount { get { return EntryErrors.Count; } }
		public int CommentErrorCount { get { return CommentErrors.Count; } }
		public int Count { get { return EntryErrorCount + CommentErrorCount; } }

		public void AddEntryError(TEntry entry, Exception error) {
			EntryErrors.Add(new EntryConversionError<TEntry>(entry, error));
		}

		public void AddCommentError(LfComment comment, Exception error, EntryConversionError<TEntry> entryError ) {
			CommentErrors.Add(new CommentConversionError<TEntry>(comment, error, entryError));
		}

		public void AddCommentError(LfComment comment, Exception error, TEntry entry) {
			var entryError = new EntryConversionError<TEntry>(entry, null);
			CommentErrors.Add(new CommentConversionError<TEntry>(comment, error, entryError));
		}

		public void AddCommentErrors(List<CommentConversionError<TEntry>> commentErrors) {
			CommentErrors.AddRange(commentErrors);
		}
	}

	public class EntryConversionError<TEntry>
	{
		public TEntry Entry { get; set; }
		public Exception Error { get; set; }

		public EntryConversionError(TEntry entry, Exception error) {
			Entry = entry;
			Error = error;
		}

		public Guid EntryGuid() {
			if (Entry == null) return Guid.Empty;
			LfLexEntry lfEntry = Entry as LfLexEntry;
			if (lfEntry != null) return lfEntry.Guid ?? Guid.Empty;
			ILexEntry lcmEntry = Entry as ILexEntry;
			if (lcmEntry != null) return lcmEntry.Guid;
			return Guid.Empty;
		}

		public string MongoId() {
			if (Entry == null) return null;
			LfLexEntry lfEntry = Entry as LfLexEntry;
			if (lfEntry != null && lfEntry.Id != null) return lfEntry.Id.ToString();
			// No need to check ILexEntry as they don't have Mongo IDs
			return null;
		}

		public string Label() {
			if (Entry == null) return string.Empty;
			LfLexEntry lfEntry = Entry as LfLexEntry;
			if (lfEntry != null) return ConvertUtilities.EntryNameForDebugging(lfEntry);
			ILexEntry lcmEntry = Entry as ILexEntry;
			if (lcmEntry != null) return ConvertUtilities.EntryNameForDebugging(lcmEntry);
			return string.Empty;
		}
	}

	public class CommentConversionError<TEntry>
	{
		public LfComment Comment { get; set; }
		public Exception Error { get; set; }
		public EntryConversionError<TEntry> EntryError { get; set; }
		public Guid OptionalEntryGuid { get; set; }

		public CommentConversionError(LfComment comment, Exception error, EntryConversionError<TEntry> entryError, Guid entryGuid) {
			Comment = comment;
			Error = error;
			EntryError = entryError;
			OptionalEntryGuid = entryGuid;
		}

		public CommentConversionError(LfComment comment, Exception error, EntryConversionError<TEntry> entryError)
			: this(comment, error, entryError, Guid.Empty) { }

		public CommentConversionError(LfComment comment, Exception error, Guid entryGuid)
			: this(comment, error, null, entryGuid) { }

		public Guid EntryGuid() {
			return EntryError?.EntryGuid() ?? Guid.Empty;
		}

		public Guid CommentGuid() {
			return Comment?.Guid ?? Guid.Empty;
		}

		public string MongoId() {
			if (Comment == null || Comment.Id == null) return null;
			return Comment.Id.ToString();
		}

		public string Label() {
			if (Comment == null || Comment.Content == null) return string.Empty;
			return Comment.Content.Substring(0, 100);
		}
	}
}