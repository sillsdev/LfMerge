// Copyright (c) 2022 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.LanguageForge.Model;
using SIL.LCModel;

namespace LfMerge.Core.Reporting
{
    public class ErrorReport
    {
        public DateTime Timestamp { get; set; }

        public string ExceptionMessage { get; set; }

        public ConversionErrorReport Skipped { get; set; }

        public static ErrorReport Create(ConversionErrorReport skipped, Exception overallException)
        {
            return new ErrorReport {
                Timestamp = DateTime.UtcNow,
                ExceptionMessage = overallException?.ToString(),
                Skipped = skipped
            };
        }

        public static ErrorReport Create(ConversionErrorReport skipped)
        {
            return ErrorReport.Create(skipped, null);
        }
    }

    public class ConversionErrorReport
    {
        public ConversionErrors ToMongo { get; set; }
        public ConversionErrors ToLcm { get; set; }

        public static ConversionErrorReport Create(ConversionErrors toMongoErrors, ConversionErrors toLcmErrors)
        {
            return new ConversionErrorReport {
                ToMongo = toMongoErrors,
                ToLcm = toLcmErrors
            };
        }
    }

    public class ConversionErrors
    {
        public ErrorList Entries { get; set; }
        public ErrorList Comments { get; set; }

        public static ConversionErrors Create(
            IEnumerable<Tuple<ILexEntry, Exception>> entryErrors,
            IEnumerable<Tuple<LfComment, Exception, ILexEntry>> commentErrors)
        {
            return new ConversionErrors {
                Entries = ErrorList.CreateEntryList(entryErrors),
                Comments = ErrorList.CreateCommentList(commentErrors)
            };
        }

        public static ConversionErrors Create(
            IEnumerable<Tuple<LfLexEntry, Exception>> entryErrors,
            IEnumerable<Tuple<LfComment, Exception, LfLexEntry, Exception>> commentErrors)
        {
            return new ConversionErrors {
                Entries = ErrorList.CreateEntryList(entryErrors),
                Comments = ErrorList.CreateCommentList(commentErrors)
            };
        }
    }

    public class ErrorList
    {
        public int Count { get { return List?.Count ?? 0; } }
        public IList<SingleItemReport> List { get; set; }

        public static ErrorList CreateEntryList(IEnumerable<Tuple<LfLexEntry, Exception>> t)
        {
            return new ErrorList {
                List = t.Select(SingleItemReport.CreateEntryReport).ToList()
            };
        }

        public static ErrorList CreateEntryList(IEnumerable<Tuple<ILexEntry, Exception>> t)
        {
            return new ErrorList {
                List = t.Select(SingleItemReport.CreateEntryReport).ToList()
            };
        }

        public static ErrorList CreateCommentList(IEnumerable<Tuple<LfComment, Exception, LfLexEntry, Exception>> t)
        {
            return new ErrorList {
                List = t.Select(SingleItemReport.CreateCommentReport).ToList()
            };
        }
        public static ErrorList CreateCommentList(IEnumerable<Tuple<LfComment, Exception, ILexEntry>> t)
        {
            return new ErrorList {
                List = t.Select(SingleItemReport.CreateCommentReport).ToList()
            };
        }
    }

	public class SingleItemReport
	{
        public Guid Guid { get; set; }
        public string Id { get; set; }
        public string ExceptionMessage { get; set; }
        public string Label { get; set; }
        public SingleItemReport Entry { get; set; }

        public bool ShouldSerializeEntry() { return Entry != null; }

        public static SingleItemReport CreateEntryReport(Tuple<LfLexEntry, Exception> t)
        {
            return new SingleItemReport {
                Guid = t.Item1.Guid ?? Guid.Empty,
                Id = null,
                ExceptionMessage = t.Item2?.ToString(),
                Label = DataConverters.ConvertUtilities.EntryNameForDebugging(t.Item1),
                Entry = null
            };
        }

        public static SingleItemReport CreateEntryReport(Tuple<ILexEntry, Exception> t)
        {
            return new SingleItemReport {
                Guid = t.Item1.Guid,
                Id = null,
                ExceptionMessage = t.Item2?.ToString(),
                Label = DataConverters.ConvertUtilities.EntryNameForDebugging(t.Item1),
                Entry = null
            };
        }

        public static SingleItemReport CreateCommentReport(Tuple<LfComment, Exception, LfLexEntry, Exception> t)
        {
            return new SingleItemReport {
                Guid = t.Item1.Guid ?? Guid.Empty,
                Id = t.Item1.Id.ToString(),
                ExceptionMessage = t.Item2?.ToString(),
                Label = t.Item1.Content?.Substring(0, 100) ?? "<empty comment>",
                Entry = SingleItemReport.CreateEntryReport(Tuple.Create(t.Item3, t.Item4))
            };
        }

        public static SingleItemReport CreateCommentReport(Tuple<LfComment, Exception, ILexEntry> t)
        {
            return new SingleItemReport {
                Guid = t.Item1.Guid ?? Guid.Empty,
                Id = t.Item1.Id.ToString(),
                ExceptionMessage = t.Item2?.ToString(),
                Label = t.Item1.Content?.Substring(0, 100) ?? "<empty comment>",
                Entry = SingleItemReport.CreateEntryReport(Tuple.Create(t.Item3, null as Exception))
            };
        }
	}
}