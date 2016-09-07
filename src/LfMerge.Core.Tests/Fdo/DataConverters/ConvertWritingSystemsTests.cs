// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Fdo.DataConverters
{
	public class ConvertWritingSystemsTests : FdoTestBase
	{
		public ConvertWritingSystemsTests()
		{
		}

		[Test]
		public void WritingSystemManager_AnalysisPlusVernacularPlusPronunciation_ShouldEqualAll()
		{
			// TODO: Better name for this test
			// TODO: Stop using "public" member D that's going to become private

			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, TestProjectCode);
			var cache = lfProject.FieldWorksProject.Cache;
			var sut = new LfMerge.Core.DataConverters.ConvertWritingSystems(cache);
			Console.WriteLine("Analysis");
			foreach (var kv in sut.AnalysisWritingSystems)
			{
				Console.WriteLine("{0} => {1}", kv.Key, kv.Value);
			}
			Console.WriteLine("Pronunciation");
			foreach (var kv in sut.PronunciationWritingSystems)
			{
				Console.WriteLine("{0} => {1}", kv.Key, kv.Value);
			}
			Console.WriteLine("Vernacular");
			foreach (var kv in sut.VernacularWritingSystems)
			{
				Console.WriteLine("{0} => {1}", kv.Key, kv.Value);
			}
		}
	}
}

