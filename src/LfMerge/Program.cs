// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using LfMerge.FieldWorks;

namespace LfMerge
{
	class MainClass
	{
		[STAThread]
		public static void Main(string[] args)
		{
			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			var database = args.Length > 1 ? args[0] : "Sena 3";

			// TODO: read settings from config instead of hard coding them here
			var baseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			LfMergeDirectories.Initialize(baseDir);

			using (var fw = new FwProject(LfMergeDirectories.Current, database))
			{
				// just some test output
				var fdoCache = fw.Cache;
				Console.WriteLine("Ethnologue Code: {0}", fdoCache.LangProject.EthnologueCode);
				Console.WriteLine("Interlinear texts:");
				foreach (var t in fdoCache.LangProject.InterlinearTexts)
				{
					Console.WriteLine("{0:D6}: title: {1} (comment: {2})", t.Hvo,
						t.Title.BestVernacularAlternative.Text,
						t.Comment.BestVernacularAnalysisAlternative.Text);
				}
			}
		}
	}
}
