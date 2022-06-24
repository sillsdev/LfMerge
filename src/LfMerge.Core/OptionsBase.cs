// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace LfMerge.Core
{
    public abstract class OptionsBase<T>
    {
		protected static IList<Error> _parseErrors = new List<Error>();

        public static T Current;

		public IEnumerable<Error> ParseErrors { get { return _parseErrors; } }

		public static T ParseCommandLineArgs(string[] args)
		{
			T options = default(T);
			Parser.Default.ParseArguments<T>(args)
				.WithParsed(o => {
					Current = o;
					options = o;
					_parseErrors.Clear();
				})
				.WithNotParsed(e => _parseErrors = e.ToList());

			return options;
		}
    }
}