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

        public ErrorReport WithSkipped(ConversionErrorReport skipped)
        {
            return new ErrorReport {
                Timestamp = DateTime.UtcNow,
                ExceptionMessage = ExceptionMessage,
                Skipped = skipped
            };
        }

        public static ErrorReport CreateOrUpdate(ErrorReport orig, ConversionErrorReport skipped)
        {
			if (orig == null) {
				return new ErrorReport {
					Timestamp = DateTime.UtcNow,
					ExceptionMessage = null,
					Skipped = skipped
				};
			} else {
				return new ErrorReport {
					Timestamp = DateTime.UtcNow,
					ExceptionMessage = orig.ExceptionMessage,
					Skipped = skipped
				};
			}
        }

        public static ErrorReport WithMongoErrors(ErrorReport orig, ConversionError<ILexEntry> toMongoErrors)
        {
			var skipped = orig?.Skipped ?? ConversionErrorReport.FromMongoConversionError(toMongoErrors);
			return ErrorReport.CreateOrUpdate(orig, skipped);
        }

        public static ErrorReport WithLcmErrors(ErrorReport orig, ConversionError<LfLexEntry> toLcmErrors)
        {
			var skipped = orig?.Skipped ?? ConversionErrorReport.FromLcmConversionError(toLcmErrors);
			return ErrorReport.CreateOrUpdate(orig, skipped);
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

        public static ConversionErrorReport FromMongoConversionError<T>(ConversionError<T> mongoErrors)
        {
            return new ConversionErrorReport {
                ToMongo = ConversionErrors.FromConversionError(mongoErrors),
				ToLcm = null,
            };
        }

        public static ConversionErrorReport FromLcmConversionError<T>(ConversionError<T> lcmErrors)
        {
            return new ConversionErrorReport {
                ToMongo = null,
				ToLcm = ConversionErrors.FromConversionError(lcmErrors),
            };
        }
    }

    public class ConversionErrors
    {
        public ErrorList Entries { get; set; }
        public ErrorList Comments { get; set; }

        public static ConversionErrors FromConversionError<T>(
			ConversionError<T> errorSet)
        {
            return new ConversionErrors {
                Entries = ErrorList.FromEntryErrors<T>(errorSet.EntryErrors),
				Comments = ErrorList.FromCommentErrors<T>(errorSet.CommentErrors),
            };
        }
    }

    public class ErrorList
    {
        public int Count { get { return List?.Count ?? 0; } }
        public IList<SingleItemReport> List { get; set; }

		public static ErrorList FromEntryErrors<T>(List<EntryConversionError<T>> entryErrors)
		{
			return new ErrorList {
				List = entryErrors.Select(SingleItemReport.FromEntryError<T>).ToList()
			};
		}

		public static ErrorList FromCommentErrors<T>(List<CommentConversionError<T>> commentErrors)
		{
			return new ErrorList {
				List = commentErrors.Select(SingleItemReport.FromCommentError<T>).ToList()
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

		public static SingleItemReport FromEntryError<T>(EntryConversionError<T> error)
		{
			return new SingleItemReport {
				Guid = error.EntryGuid(),
				Id = null,
				ExceptionMessage = error.Error?.ToString(),
				Label = error.Label(),
				Entry = null
			};
		}

		public static SingleItemReport FromCommentError<T>(CommentConversionError<T> error)
		{
			return new SingleItemReport {
				Guid = error.CommentGuid(),
				Id = error.MongoId(),
				ExceptionMessage = error.Error?.ToString(),
				Label = error.Label(),
				Entry = error.EntryError == null ? null : FromEntryError<T>(error.EntryError),
			};
		}
	}
}