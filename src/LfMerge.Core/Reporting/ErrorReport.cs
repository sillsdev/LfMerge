// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;

namespace LfMerge.Core.Reporting
{
    public class ErrorReport
    {
        public DateTime Timestamp { get; set; }

        public string ExceptionMessage { get; set; }

        public ConversionErrorReport Skipped { get; set; }
    }

    public class ConversionErrorReport
    {
        public ConversionErrors ToMongo { get; set; }
        public ConversionErrors ToLcm { get; set; }

    }

    public class ConversionErrors 
    {
        public ErrorList Entries { get; set; }
        public ErrorList Comments { get; set; }
    }

    public class ErrorList
    {
        public int Count { get { return List?.Count ?? 0; } }
        public IList<SingleItemReport> List { get; set; }
    }

	public class SingleItemReport
	{
        public Guid Guid { get; set; }
        public string Id { get; set; }
        public string ExceptionMessage { get; set; }
        public string Label { get; set; }
        public SingleItemReport Entry { get; set; }

        public bool ShouldSerializeEntry()
        {
            return Entry != null;
        }
	}
}