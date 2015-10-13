// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;

namespace LfMerge
{
	class MainClass
	{
		[STAThread]
		public static void Main(string[] args)
		{
			// TODO: define and process program arguments
			var database = args.Length > 1 ? args[0] : "Sena 3";

			// TODO: read settings from config instead of hard coding them here
			var baseDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "fwrepo/fw/DistFiles");
			var fdoDirs = new LfMergeDirectories(baseDir, "ReleaseData", "Templates");
			var proj = new ProjectIdentifier(fdoDirs, database);

			using (var fw = new FwAccess(proj))
			{
				fw.UpdateFdoFromMongoDb();
				fw.RunSendReceive();
				fw.UpdateMongoDbFromFdo();

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
