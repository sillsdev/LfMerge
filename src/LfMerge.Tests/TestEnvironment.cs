// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using NUnit.Framework;
using Palaso.TestUtilities;
using System.Collections.Generic;
//using System.Xml;
using System.Text;

namespace LfMerge.Tests
{
	class TestEnvironment : IDisposable
	{
		private readonly TemporaryFolder _languageForgeServerFolder;

		public TestEnvironment()
		{
			_languageForgeServerFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name
				+ Path.GetRandomFileName());
			LfMergeDirectories.Initialize(LanguageForgeFolder);
		}

		public void Dispose()
		{
			_languageForgeServerFolder.Dispose();
		}

		public string LanguageForgeFolder
		{
			get { return _languageForgeServerFolder.Path; }
		}

		public LfMergeDirectories LangForgeDirFinder
		{
			get { return LfMergeDirectories.Current; }
		}

		public string ProjectPath(string projectName)
		{
			return Path.Combine(LanguageForgeFolder, projectName);
		}

		public void CreateProjectUpdateFolder(string projectName)
		{
			Directory.CreateDirectory(ProjectPath(projectName));
		}

//		public string WriteFile(string fileName, string xmlForEntries, string directory)
//		{
//			string content;
//			using (var writer = File.CreateText(Path.Combine(directory, fileName)))
//			{
//				content = string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?> " +
//					"<lift version =\"{0}\" producer=\"WeSay.1Pt0Alpha\" " +
//					"xmlns:flex=\"http://fieldworks.sil.org\">{1}" +
//					"</lift>", Validator.LiftVersion, xmlForEntries);
//				writer.Write(content);
//			}
//
//			new FileInfo(Path.Combine(directory, fileName)).LastWriteTime = DateTime.Now.AddSeconds(1);
//
//			return content;
//		}
//
//		public void CreateLiftInputFile(IList<string> data, string fileName, string directory)
//		{
//			// NOTE: if the parameter differentTimeStamps is true we wait a second before
//			// creating the next file. This allows the files to have different timestamps.
//			// Originally the code used 100ms instead of 1s, but the resolution of file
//			// timestamps in the Mono implementation is 1s. However, if is possible in the
//			// real app that two files get created within a few milliseconds and we rely on
//			// the file timestamp to do the sorting of files, then we have a real problem.
//
//			var path = Path.Combine(directory, fileName);
//			if (File.Exists(path))
//				File.Delete(path);
//			using (var wrtr = File.CreateText(path))
//			{
//				for (var i = 0; i < data.Count; ++i)
//					wrtr.WriteLine(data[i]);
//				wrtr.Close();
//			}
//		}
//
//		public void CreateLiftUpdateFile(IList<string> data, string fileName,
//			string directory)
//		{
//			string path = Path.Combine(directory, fileName);
//			if (File.Exists(path))
//				File.Delete(path);
//			var bldr = new StringBuilder();
//			for (var i = 0; i < data.Count; ++i)
//				bldr.AppendLine(data[i]);
//
//			WriteFile(fileName, bldr.ToString(), directory);
//		}

//		public void VerifyEntryInnerText(XmlDocument xmlDoc, string xPath, string innerText)
//		{
//			var selectedEntries = VerifyEntryExists(xmlDoc, xPath);
//			Assert.That(selectedEntries[0].InnerText, Is.EqualTo(innerText), "Text for entry is wrong");
//		}
//
//		public XmlNodeList VerifyEntryExists(XmlDocument xmlDoc, string xPath)
//		{
//			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
//			Assert.IsNotNull(selectedEntries);
//			Assert.AreEqual(1, selectedEntries.Count,
//				String.Format("An entry with the following criteria should exist:{0}", xPath));
//			return selectedEntries;
//		}
//
//		public void VerifyEntryDoesNotExist(XmlDocument xmlDoc, string xPath)
//		{
//			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
//			Assert.IsNotNull(selectedEntries);
//			Assert.AreEqual(0, selectedEntries.Count,
//				String.Format("An entry with the following criteria should not exist:{0}", xPath));
//		}
	}
}

