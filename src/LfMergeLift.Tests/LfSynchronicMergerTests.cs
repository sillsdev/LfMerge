// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.Lift.Validation;
using Palaso.TestUtilities;

namespace LfMergeLift.Tests
{
	[TestFixture]
	public class LfSynchronicMergerTests
	{
		#region TestEnvironment
		private class TestEnvironment : IDisposable
		{
			private readonly TemporaryFolder _languageForgeServerFolder =
				new TemporaryFolder("LangForge" + Path.GetRandomFileName());

			public void Dispose()
			{
				_languageForgeServerFolder.Dispose();
			}

			internal void CreateLiftInputFile(IList<string> data, string fileName, string directory)
			{
				var path = Path.Combine(directory, fileName);
				if (File.Exists(path))
					File.Delete(path);
				using (var wrtr = File.CreateText(path))
				{
					for (var i = 0; i < data.Count; ++i)
						wrtr.WriteLine(data[i]);
					wrtr.Close();
				}

				//pause so they don't all have the same time
				Thread.Sleep(100);
			}
			internal void CreateLiftUpdateFile(IList<string> data, string fileName, string directory)
			{
				string path = Path.Combine(directory, fileName);
				if (File.Exists(path))
					File.Delete(path);
				using (var wrtr = File.CreateText(path))
				{
					wrtr.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
					wrtr.WriteLine("<lift version =\"{0}\" producer=\"WeSay.1Pt0Alpha\" " +
						"xmlns:flex=\"http://fieldworks.sil.org\">", Validator.LiftVersion);
					for (var i = 0; i < data.Count; ++i)
						wrtr.WriteLine(data[i]);
					wrtr.WriteLine("</lift>");
					wrtr.Close();
				}

				//pause so they don't all have the same time
				Thread.Sleep(100);
			}

			internal string WriteFile(string fileName, string xmlForEntries, string directory)
			{
				string content;
				using (var writer = File.CreateText(Path.Combine(directory, fileName)))
				{
					content = string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?> " +
						"<lift version =\"{0}\" producer=\"WeSay.1Pt0Alpha\" " +
						"xmlns:flex=\"http://fieldworks.sil.org\">{1}" +
						"</lift>", Validator.LiftVersion, xmlForEntries);
					writer.Write(content);
				}

				new FileInfo(Path.Combine(directory, fileName)).LastWriteTime = DateTime.Now.AddSeconds(1);
				//pause so they don't all have the same time
				Thread.Sleep(100);

				return content;
			}

			internal void VerifyEntryInnerText(XmlDocument xmlDoc, string xPath, string innerText)
			{
				var selectedEntries = VerifyEntryExists(xmlDoc, xPath);
				XmlNode entry = selectedEntries[0];
				Assert.AreEqual(innerText, entry.InnerText, String.Format("Text for entry is wrong"));
			}

			internal XmlNodeList VerifyEntryExists(XmlDocument xmlDoc, string xPath)
			{
				XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
				Assert.IsNotNull(selectedEntries);
				Assert.AreEqual(1, selectedEntries.Count, String.Format("An entry with the following criteria should exist:{0}", xPath));
				return selectedEntries;
			}

			internal void VerifyEntryDoesNotExist(XmlDocument xmlDoc, string xPath)
			{
				XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
				Assert.IsNotNull(selectedEntries);
				Assert.AreEqual(0, selectedEntries.Count,
								String.Format("An entry with the following criteria should not exist:{0}", xPath));
			}
		} //END class TestEnvironment
		#endregion
		//=============================================================================================================================

		private const string _baseLiftFileName = "base.lift";

		private string _directory;
		private LfSynchronicMerger _merger;

		private string LiftFilePath
		{
			get { return Path.Combine(_directory, _baseLiftFileName); }
		}

		[SetUp]
		public void Setup()
		{
			_merger = new LfSynchronicMerger();
			_directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(_directory);
		}

		[TearDown]
		public void TearDown()
		{
			Directory.Delete(_directory, true);
		}

		static private readonly string[] s_LiftMainFile = new[]
		{
			"<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
			"<lift producer=\"SIL.FLEx 7.3.0.41038\" version=\"0.13\">",
			"<header>",
			"<ranges>",
			"<range id=\"dialect\" href=\"file://C:/Users/maclean/Documents/aa Work/LIFToutput/LIFToutput.lift-ranges\"/>",
			"</ranges>",
			"<fields>",
			"<field tag=\"summary-definition\">",
			"<form lang=\"en\"><text>A summary definition (located at the entry level in the Entry pane) is a general definition summarizing all the senses of a primary entry. It has no theoretical value; its use is solely pragmatic.</text></form>",
			"</field>",

			"<field tag=\"scientific-name\">",
			"<form lang=\"en\"><text>This field stores the scientific name pertinent to the current sense.</text></form>",
			"</field>",
			"</fields>",
			"</header>",

			//entry 1
			"<entry dateCreated=\"2012-04-27T20:03:06Z\" dateModified=\"2012-05-09T06:56:33Z\" id=\"chair_22db1bfd-aa70-488d-adad-ac3932e6a708\" guid=\"22db1bfd-aa70-488d-adad-ac3932e6a708\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>chair</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"d5fd85b6-24cf-4a20-9156-9b1ee3959714\" order=\"0\">",
			"<gloss lang=\"en\"><text>seat</text></gloss>",
			"<definition>",
			"<form lang=\"en\"><text>Furniture people sit on which has four legs</text></form>",
			"</definition>",
			"<relation type=\"Part\" ref=\"9a3b501a-b487-47c1-b77b-41975c7147d2\"/>",
			"</sense>",

			"<sense id=\"db1e3397-befd-46f6-bdf5-f9039cf5030e\" order=\"1\">",
			"<gloss lang=\"en\"><text>stool</text></gloss>",
			"<definition>",
			"<form lang=\"en\"><text>Furniture that people sit on in a somewhat stading position and can have 3 or 4 legs</text></form>",
			"</definition>",
			"</sense>",
			"</entry>",

			//entry 2
			"<entry dateCreated=\"2012-04-23T16:50:57Z\" dateModified=\"2012-05-09T06:52:16Z\" id=\"dog_25a9e770-8298-4547-9f8b-147ea70cb42a\" guid=\"25a9e770-8298-4547-9f8b-147ea70cb42a\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>dog</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"1b33697f-91e1-4b57-bab7-824b74d04f86\">",
			"<gloss lang=\"en\"><text>doggy</text></gloss>",
			"<definition>",
			"<form lang=\"en\"><text>doggy that is a pet</text></form>",
			"</definition>",
			"<relation type=\"Part\" ref=\"6d20a75d-0c74-432e-a169-7042fcd6f026\"/>",
			"</sense>",
			"</entry>",

			//entry 3
			"<entry dateCreated=\"2012-04-27T16:49:14Z\" dateModified=\"2012-05-04T03:05:38Z\" id=\"pike_316611bc-df2b-4e4a-9bf6-d240c3ce31db\" guid=\"316611bc-df2b-4e4a-9bf6-d240c3ce31db\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>pike</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\">",
			"<gloss lang=\"en\"><text>one awsome catch</text></gloss>",
			"<relation type=\"Whole\" ref=\"7ddb62da-fa55-404f-b944-46b71b00c8c8\"/>",
			"</sense>",
			"</entry>",

			//entry 4
			"<entry dateCreated=\"2012-05-04T03:05:03Z\" dateModified=\"2012-05-04T03:05:50Z\" id=\"fish_7026c804-799b-4cd2-861f-c8f71cfa9f93\" guid=\"7026c804-799b-4cd2-861f-c8f71cfa9f93\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>fish</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"7ddb62da-fa55-404f-b944-46b71b00c8c8\">",
			"<grammatical-info value=\"Noun\">",
			"</grammatical-info>",
			"<gloss lang=\"en\"><text>swimming creature</text></gloss>",
			"<relation type=\"Part\" ref=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\"/>",
			"</sense>",
			"</entry>",

			//entry 5
			"<entry dateCreated=\"2012-04-23T16:50:51Z\" dateModified=\"2012-05-09T06:54:13Z\" id=\"cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8\" guid=\"8338bdd5-c1c2-46b2-93d1-2328cbb749c8\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>cat</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"9aaf4b46-f2b5-452f-981f-8517e64e6dc2\">",
			"<gloss lang=\"en\"><text>meuwer</text></gloss>",
			"<gloss lang=\"es\"><text>cataeouw</text></gloss>",
			"<example source=\"dsd reference for Example\">",
			"<form lang=\"fr\"><text>ExampleSentence </text></form>",
			"<form lang=\"frc\"><text>Another ws example sentence</text></form>",
			"<translation type=\"Free translation\">",
			"<form lang=\"en\"><text>This is a translation of example sentences</text></form>",
			"<form lang=\"es\"><text>In another ws this is a translation of exSentences</text></form>",
			"</translation>",
			"<note type=\"reference\">",
			"<form lang=\"en\"><text>dsd reference for Example</text></form>",
			"</note>",
			"</example>",
			"<example source=\"reference for second translation\">",
			"<form lang=\"fr\"><text>Second example sentence.</text></form>",
			"<form lang=\"frc\"><text>Other lang second example.</text></form>",
			"<translation type=\"Back translation\">",
			"<form lang=\"en\"><text>Second example translation</text></form>",
			"</translation>",
			"<note type=\"reference\">",
			"<form lang=\"en\"><text>reference for second translation</text></form>",
			"</note>",
			"</example>",
			"<relation type=\"Part\" ref=\"9a3b501a-b487-47c1-b77b-41975c7147d2\"/>",
			"</sense>",
			"</entry>",

			//entry 6
			"<entry dateCreated=\"2012-05-09T06:51:52Z\" dateModified=\"2012-05-09T06:52:16Z\" id=\"tail_98c54484-08a6-4136-abab-b936ddc6ad25\" guid=\"98c54484-08a6-4136-abab-b936ddc6ad25\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>tail</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"6d20a75d-0c74-432e-a169-7042fcd6f026\">",
			"<grammatical-info value=\"Noun\">",
			"</grammatical-info>",
			"<gloss lang=\"en\"><text>wagger</text></gloss>",
			"<relation type=\"Whole\" ref=\"1b33697f-91e1-4b57-bab7-824b74d04f86\"/>",
			"</sense>",
			"</entry>",

			//entry 7
			"<entry dateCreated=\"2011-03-01T18:09:46Z\" dateModified=\"2011-03-01T18:30:07Z\" guid=\"ecfbe958-36a1-4b82-bb69-ca5210355400\" id=\"hombre_ecfbe958-36a1-4b82-bb69-ca5210355400\">",
			"<lexical-unit>",
			"<form lang=\"es\"><text>hombre</text></form>",
			"<form lang=\"fr-Zxxx-x-AUDIO\"><text>hombre634407358826681759.wav</text></form>",
			"<form lang=\"Fr-Tech 30Oct\"><text>form in bad WS</text></form>",
			"</lexical-unit>",
			"<trait name=\"morph-type\" value=\"root\"></trait>",
			"<pronunciation>",
			"<form lang=\"fr\"><text>ombre</text></form>",
			"<media href=\"Sleep Away.mp3\">",
			"</media>",
			"</pronunciation>",
			"<sense id=\"hombre_f63f1ccf-3d50-417e-8024-035d999d48bc\">",
			"<grammatical-info value=\"Noun\">",
			"</grammatical-info>",
			"<gloss lang=\"en\"><text>man</text></gloss>",
			"<definition>",
			"<form lang=\"en\"><text>male adult human <span href=\"file://others/SomeFile.txt\" class=\"Hyperlink\">link</span></text></form>",
			"<form lang=\"fr-Zxxx-x-AUDIO\"><text>male adult634407358826681760.wav</text></form>",
			"</definition>",
			"<illustration href=\"Desert.jpg\">",
			"<label>",
			"<form lang=\"fr\"><text>Desert</text></form>",
			"</label>",
			"</illustration>",
			"<illustration href=\"subfolder/MyPic.jpg\">",
			"<label>",
			"<form lang=\"fr\"><text>My picture</text></form>",
			"</label>",
			"</illustration>",
			"<trait name=\"semantic-domain-ddp4\" value=\"2.6.5.1 Man\"></trait>",
			"<trait name=\"semantic-domain-ddp4\" value=\"2.6.4.4 Adult\"></trait>",
			"</sense>",
			"<sense id=\"creature7ddb62da-fa55-404f-b944-46b71b00c8c8\">",
			"<grammatical-info value=\"Noun\">",
			"</grammatical-info>",
			"<gloss lang=\"en\"><text>swimming creature</text></gloss>",
			"<relation type=\"Part\" ref=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\"/>",
			"</sense>",
			"</entry>",

			//entry 8
			"<entry dateCreated=\"2012-05-09T06:53:05Z\" dateModified=\"2012-05-09T06:53:53Z\" id=\"leg_d6b29be3-a278-4c5f-9c43-2de7cc820e4f\" guid=\"d6b29be3-a278-4c5f-9c43-2de7cc820e4f\">",
			"<lexical-unit>",
			"<form lang=\"fr\"><text>leg</text></form>",
			"</lexical-unit>",
			"<trait  name=\"morph-type\" value=\"stem\"/>",
			"<sense id=\"9a3b501a-b487-47c1-b77b-41975c7147d2\">",
			"<grammatical-info value=\"Noun\">",
			"</grammatical-info>",
			"<gloss lang=\"en\"><text>leg</text></gloss>",
			"<definition>",
			"<form lang=\"en\"><text>Part of chair or table or animal</text></form>",
			"</definition>",
			"<relation type=\"Whole\" ref=\"d5fd85b6-24cf-4a20-9156-9b1ee3959714\"/>",
			"<relation type=\"Whole\" ref=\"9aaf4b46-f2b5-452f-981f-8517e64e6dc2\"/>",
			"</sense>",
			"</entry>",

			"</lift>"
		};

		[Test]
		public void AddSenseAndRemoveSense()
		{
			var liftUpdateAddSenseAndRemoveSense = new[]
			{
				"<entry dateCreated=\"2011-03-01T18:09:46Z\" dateModified=\"2012-05-12T18:30:07Z\" guid=\"ecfbe958-36a1-4b82-bb69-ca5210355400\" id=\"hombre_ecfbe958-36a1-4b82-bb69-ca5210355400\">",
				"<lexical-unit>",
				"<form lang=\"es\"><text>hombre</text></form>",
				// removed "<form lang=\"fr-Zxxx-x-AUDIO\"><text>hombre634407358826681759.wav</text></form>",
				"<form lang=\"Fr-Tech 30Oct\"><text>form in bad WS BUT STILL CHANGED</text></form>", //changed
				"</lexical-unit>",
				//"<trait name=\"morph-type\" value=\"root\"></trait>",
				//"<pronunciation>",
				//"<form lang=\"fr\"><text>ombre</text></form>",
				//"<media href=\"Sleep Away.mp3\">",
				//"</media>",
				//"</pronunciation>",
				"<sense id=\"hombre_f63f1ccf-3d50-417e-8024-035d999d48bc\">",
					"<grammatical-info value=\"Noun-CHANGED\">",
					"</grammatical-info>",
					"<gloss lang=\"en\"><text>man</text></gloss>",
						"<definition>",
						"<form lang=\"en\"><text>male adult human SPAN WAS REMOVED</text></form>",
						"<form lang=\"fr-Zxxx-x-AUDIO\"><text>male adult634407358826681760.wav</text></form>",
						"</definition>",
					//"<illustration href=\"Desert.jpg\">",
					//"<label>",
					//"<form lang=\"fr\"><text>Desert</text></form>",
					//"</label>",
					//"</illustration>",
					//"<illustration href=\"subfolder/MyPic.jpg\">",
					//"<label>",
					//"<form lang=\"fr\"><text>My picture</text></form>",
					//"</label>",
					//"</illustration>",
					//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.5.1 Man\"></trait>",
					//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.4.4 Adult\"></trait>",
				"</sense>",

				"<sense id=\"stool3397-befd-46f6-bdf5-f9039cf5030e\" order=\"1\">",
					"<gloss lang=\"en\"><text>stool</text></gloss>",
						"<definition>",
						"<form lang=\"en\"><text>SEE that THIS SENSE WAS ADDED into this ENTRY</text></form>",
						"</definition>",
				"</sense>",
				"</entry>"
			};

			using (var env = new TestEnvironment())
			{
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateAddSenseAndRemoveSense,
									 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);

				//Run SynchronicMerger
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				var howmany = doc.SelectNodes("//entry").Count;
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));
				XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='hombre_ecfbe958-36a1-4b82-bb69-ca5210355400']");
				Assert.IsNotNull(changedEntries);
				Assert.AreEqual(1, changedEntries.Count);
				XmlNodeList senses = changedEntries[0].SelectNodes("sense");
				Assert.IsNotNull(senses);
				Assert.AreEqual(2, senses.Count);
				var senseId = senses.Item(0).Attributes["id"].Value;
				Assert.AreEqual("hombre_f63f1ccf-3d50-417e-8024-035d999d48bc", senseId);
				senseId = senses.Item(1).Attributes["id"].Value;
				Assert.AreEqual("stool3397-befd-46f6-bdf5-f9039cf5030e", senseId);

				//leg_d6b29be3-a278-4c5f-9c43-2de7cc820e4f
				//Ensure the following node was not somehow removed.
				XmlNodeList untouchedNode = doc.SelectNodes("//entry[@id='leg_d6b29be3-a278-4c5f-9c43-2de7cc820e4f']");
				Assert.IsNotNull(untouchedNode);

				//Make sure all the nodes are still there.
				untouchedNode = doc.SelectNodes("//entry[@id='chair_22db1bfd-aa70-488d-adad-ac3932e6a708']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='dog_25a9e770-8298-4547-9f8b-147ea70cb42a']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='pike_316611bc-df2b-4e4a-9bf6-d240c3ce31db']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='fish_7026c804-799b-4cd2-861f-c8f71cfa9f93']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='tail_98c54484-08a6-4136-abab-b936ddc6ad25']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='hombre_ecfbe958-36a1-4b82-bb69-ca5210355400']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
				untouchedNode = doc.SelectNodes("//entry[@id='leg_d6b29be3-a278-4c5f-9c43-2de7cc820e4f']");
				Assert.IsNotNull(untouchedNode); Assert.AreEqual(1, untouchedNode.Count);
			}
		}

		[Test]
		public void DeleteEntry()
		{
			var liftUpdateEntryDeleted = new[]
			{
				"<entry dateCreated=\"2012-05-04T03:05:03Z\" dateModified=\"2012-05-04T03:05:50Z\" id=\"fish_7026c804-799b-4cd2-861f-c8f71cfa9f93\" guid=\"7026c804-799b-4cd2-861f-c8f71cfa9f93\" dateDeleted=\"2012-05-08T06:40:44Z\">",
				"</entry>"
			};
			using (var env = new TestEnvironment())
			{
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateEntryDeleted,
									 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				var numberOfEntries = doc.SelectNodes("//entry").Count;
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));
				Assert.That(doc.SelectNodes("//entry[@id='fish_7026c804-799b-4cd2-861f-c8f71cfa9f93']"), Has.Count.EqualTo(1));

				XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='fish_7026c804-799b-4cd2-861f-c8f71cfa9f93']");
				XmlNode deletedNode = changedEntries[0];
				Assert.IsNullOrEmpty(deletedNode.InnerXml);
				var deletionDate = deletedNode.Attributes["dateDeleted"].Value;
				Assert.AreEqual(deletionDate, "2012-05-08T06:40:44Z");
			}
		}

		[Test]
		public void ChangeDateAndLexicalUnit()
		{
			var liftUpdateDateAndLexicalUnitChanged = new[]
			{
				//entry7
				//changed dateModified=\"2012-05-12T18:30:07Z\"
				"<entry dateCreated=\"2011-03-01T18:09:46Z\" dateModified=\"2012-05-12T18:30:07Z\" guid=\"ecfbe958-36a1-4b82-bb69-ca5210355400\" id=\"hombre_ecfbe958-36a1-4b82-bb69-ca5210355400\">",
					"<lexical-unit>",
					"<form lang=\"es\"><text>hombre</text></form>",
					// removed "<form lang=\"fr-Zxxx-x-AUDIO\"><text>hombre634407358826681759.wav</text></form>",
					"<form lang=\"Fr-Tech 30Oct\"><text>form in bad WS BUT STILL CHANGED</text></form>",  //changed
					"</lexical-unit>",
					//"<trait name=\"morph-type\" value=\"root\"></trait>",
					//"<pronunciation>",
					//"<form lang=\"fr\"><text>ombre</text></form>",
					//"<media href=\"Sleep Away.mp3\">",
					//"</media>",
					//"</pronunciation>",
						"<sense id=\"hombre_f63f1ccf-3d50-417e-8024-035d999d48bc\">",
							"<grammatical-info value=\"Noun\">",
							"</grammatical-info>",
							"<gloss lang=\"en\"><text>man</text></gloss>",
								"<definition>",
								"<form lang=\"en\"><text>male adult human <span href=\"file://others/SomeFile.txt\" class=\"Hyperlink\">link</span></text></form>",
								"<form lang=\"fr-Zxxx-x-AUDIO\"><text>male adult634407358826681760.wav</text></form>",
								"</definition>",
							//"<illustration href=\"Desert.jpg\">",
							//"<label>",
							//"<form lang=\"fr\"><text>Desert</text></form>",
							//"</label>",
							//"</illustration>",
							//"<illustration href=\"subfolder/MyPic.jpg\">",
							//"<label>",
							//"<form lang=\"fr\"><text>My picture</text></form>",
							//"</label>",
							//"</illustration>",
							//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.5.1 Man\"></trait>",
							//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.4.4 Adult\"></trait>",
						"</sense>",

						"<sense id=\"creature7ddb62da-fa55-404f-b944-46b71b00c8c8\">",
							"<grammatical-info value=\"Noun\">",
							"</grammatical-info>",
							"<gloss lang=\"en\"><text>swimming creature</text></gloss>",
							//"<relation type=\"Part\" ref=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\"/>",
						"</sense>",
				"</entry>"
			};
			using (var env = new TestEnvironment())
			{
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateDateAndLexicalUnitChanged,
									 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				var numberOfEntries = doc.SelectNodes("//entry").Count;
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));

				XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='hombre_ecfbe958-36a1-4b82-bb69-ca5210355400']");
				Assert.IsNotNull(changedEntries);
				Assert.AreEqual(1, changedEntries.Count);
				XmlNode changedEntry = changedEntries[0];
				var dateModified = changedEntry.Attributes["dateModified"].Value;
				Assert.AreEqual(dateModified, "2012-05-12T18:30:07Z");
				var dateCreated = changedEntry.Attributes["dateCreated"].Value;
				Assert.AreEqual(dateCreated, "2011-03-01T18:09:46Z");

				XmlNode lexUnit = changedEntry.FirstChild;
				//XmlNode lexUnit = changedEntry.SelectSingleNode(".lexical-unit");
				XmlNodeList lexicalUnitForms = lexUnit.ChildNodes;
				Assert.IsNotNull(lexicalUnitForms);
				Assert.AreEqual(2, lexicalUnitForms.Count);
				var form = lexicalUnitForms[0];
				Assert.AreEqual("hombre", form.InnerText);
				form = lexicalUnitForms[1];
				Assert.AreEqual("form in bad WS BUT STILL CHANGED", form.InnerText);
			}
		}

		[Test]
		public void ChangePOS_Gloss_Definition()
		{
			var liftUpdatePOS_Gloss_Definition_Changed = new[]
			{
				//entry7
				//changed dateModified=\"2012-05-12T18:30:07Z\"
				"<entry dateCreated=\"2011-03-01T18:09:46Z\" dateModified=\"2012-05-12T18:30:07Z\" guid=\"ecfbe958-36a1-4b82-bb69-ca5210355400\" id=\"hombre_ecfbe958-36a1-4b82-bb69-ca5210355400\">",
					"<lexical-unit>",
					"<form lang=\"es\"><text>hombre</text></form>",
					// removed "<form lang=\"fr-Zxxx-x-AUDIO\"><text>hombre634407358826681759.wav</text></form>",
					"<form lang=\"Fr-Tech 30Oct\"><text>form in bad WS</text></form>",
					"</lexical-unit>",
					//"<trait name=\"morph-type\" value=\"root\"></trait>",
					//"<pronunciation>",
					//"<form lang=\"fr\"><text>ombre</text></form>",
					//"<media href=\"Sleep Away.mp3\">",
					//"</media>",
					//"</pronunciation>",

					"<sense id=\"hombre_f63f1ccf-3d50-417e-8024-035d999d48bc\">",
						"<grammatical-info value=\"Noun-CHANGED\">",     //changed
						"</grammatical-info>",
						"<gloss lang=\"en\"><text>man-CHANGED</text></gloss>",   //changed
							"<definition>",
							"<form lang=\"en\"><text>male adult human SPAN WAS REMOVED</text></form>",               //changed
							"<form lang=\"fr-Zxxx-x-AUDIO\"><text>male adult634407358826681760.wav</text></form>",
							"</definition>",
						//"<illustration href=\"Desert.jpg\">",
						//"<label>",
						//"<form lang=\"fr\"><text>Desert</text></form>",
						//"</label>",
						//"</illustration>",
						//"<illustration href=\"subfolder/MyPic.jpg\">",
						//"<label>",
						//"<form lang=\"fr\"><text>My picture</text></form>",
						//"</label>",
						//"</illustration>",
						//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.5.1 Man\"></trait>",
						//"<trait name=\"semantic-domain-ddp4\" value=\"2.6.4.4 Adult\"></trait>",
					"</sense>",

					"<sense id=\"creature7ddb62da-fa55-404f-b944-46b71b00c8c8\">",
						"<grammatical-info value=\"Noun\">",
						"</grammatical-info>",
						"<gloss lang=\"en\"><text>swimming creature</text></gloss>",
						"<relation type=\"Part\" ref=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\"/>",
					"</sense>",
				"</entry>"
			};
			using (var env = new TestEnvironment())
			{
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdatePOS_Gloss_Definition_Changed,
									 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));

				XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='hombre_ecfbe958-36a1-4b82-bb69-ca5210355400']");
				Assert.IsNotNull(changedEntries);
				Assert.AreEqual(1, changedEntries.Count);

				XmlNodeList senses = changedEntries[0].SelectNodes("sense");
				Assert.IsNotNull(senses);
				Assert.AreEqual(2, senses.Count);
				XmlNode sense0 = senses[0];
				XmlNode sense1 = senses[1];
				var senseId = sense0.Attributes["id"].Value;
				Assert.AreEqual("hombre_f63f1ccf-3d50-417e-8024-035d999d48bc", senseId);
				senseId = sense1.Attributes["id"].Value;
				Assert.AreEqual("creature7ddb62da-fa55-404f-b944-46b71b00c8c8", senseId);

				var gramInfo = sense0.SelectSingleNode("grammatical-info").Attributes["value"].Value;
				Assert.AreEqual("Noun-CHANGED", gramInfo);
				var gloss = sense0.SelectSingleNode("gloss").InnerText;
				Assert.AreEqual("man-CHANGED", gloss);
				var def = sense0.SelectSingleNode("definition");
				var defnForms = def.SelectNodes("form");
				Assert.IsNotNull(defnForms);
				Assert.AreEqual(2, defnForms.Count);
				var form = defnForms[0];
				Assert.AreEqual("male adult human SPAN WAS REMOVED", form.InnerText);
				form = defnForms[1];
				Assert.AreEqual("male adult634407358826681760.wav", form.InnerText);

				var illustrations = sense0.SelectNodes("illustration");
				Assert.IsNotNull(illustrations);
				Assert.AreEqual(2, illustrations.Count);
				var traits = sense0.SelectNodes("trait");
				Assert.IsNotNull(traits);
				Assert.AreEqual(2, traits.Count);

				//Ensure the information that makes up the second sense has not changed.
				gramInfo = sense1.SelectSingleNode("grammatical-info").Attributes["value"].Value;
				Assert.AreEqual("Noun", gramInfo);
				gloss = sense1.SelectSingleNode("gloss").InnerText;
				Assert.AreEqual("swimming creature", gloss);
				var relation = sense1.SelectSingleNode("relation");
				Assert.IsNotNull(relation);
				//"<relation type=\"Part\" ref=\"a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d\"/>",
				Assert.AreEqual("Part", relation.Attributes["type"].Value);
				Assert.AreEqual("a3f48811-e5e4-43d0-9ce3-dcd6af3ee07d", relation.Attributes["ref"].Value);
			}
		}

		[Test]
		public void ChangeOneExampleChanged_AnotherNotChanged_OneAdded()
		{
			var liftUpdateChangeExamples = new[]
			{
				//entry 5
				"<entry dateCreated=\"2012-04-23T16:50:51Z\" dateModified=\"2012-05-09T06:54:13Z\" id=\"cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8\" guid=\"8338bdd5-c1c2-46b2-93d1-2328cbb749c8\">",
				"<lexical-unit>",
				"<form lang=\"fr\"><text>cat</text></form>",
				"</lexical-unit>",
				//"<trait  name=\"morph-type\" value=\"stem\"/>",
				"<sense id=\"9aaf4b46-f2b5-452f-981f-8517e64e6dc2\">",
				"<gloss lang=\"en\"><text>meuwer</text></gloss>",
				"<gloss lang=\"es\"><text>cataeouw</text></gloss>",

					//The LfSynchronicMerger should replace this example because some internal text is changed.
					"<example>",
					"<form lang=\"fr\"><text>ExampleSentence </text></form>",
					"<form lang=\"frc\"><text>Another ws example sentence-CHANGED</text></form>",               //change
						"<translation type=\"Free translation\">",
						"<form lang=\"en\"><text>This is a translation of example sentences-CHANGED</text></form>",    //change
						"<form lang=\"es\"><text>In another ws this is a translation of exSentences</text></form>",
						"</translation>",
					//"<note type=\"reference\">",
					//"<form lang=\"en\"><text>dsd reference for Example</text></form>",
					//"</note>",
					"</example>",

					//The LfSynchronicMerger should not replace this second example.
					"<example>",//no text change in this example but the 'source' attribute is missing
					"<form lang=\"fr\"><text>Second example sentence.</text></form>",
					"<form lang=\"frc\"><text>Other lang second example.</text></form>",
						"<translation>",
						"<form lang=\"en\"><text>Second example translation</text></form>",
						"</translation>",
					//"<note type=\"reference\">",
					//"<form lang=\"en\"><text>reference for second translation</text></form>",
					//"</note>",
					"</example>",

					//This is a third example which was added in Language Forge so it should be added to the entry
					"<example>",
					"<form lang=\"fr\"><text>Third example sentence.</text></form>",
					"<form lang=\"frc\"><text>Third other lang example.</text></form>",
						"<translation>",
						"<form lang=\"en\"><text>Third example translation</text></form>",
						"</translation>",
					"</example>",

				//"<relation type=\"Part\" ref=\"9a3b501a-b487-47c1-b77b-41975c7147d2\"/>",
				"</sense>",
				"</entry>"
			};
			using (var env = new TestEnvironment())
			{
				// Setup
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateChangeExamples,
					"LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(
					Path.Combine(_directory, _baseLiftFileName));

				// Exercise
				_merger.MergeUpdatesIntoFile(LiftFilePath, files);

				// Verify
				var doc = GetResult();
				Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(2));
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));

				var changedEntries = doc.SelectNodes("//entry[@id='cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8']");
				Assert.That(changedEntries, Has.Count.EqualTo(1));
				var changedEntry = changedEntries[0];

				var senses = changedEntry.SelectNodes("sense");
				Assert.That(senses, Has.Count.EqualTo(1));
				var sense0 = senses[0];

				Assert.That(sense0.Attributes["id"].Value, Is.EqualTo("9aaf4b46-f2b5-452f-981f-8517e64e6dc2"));
				Assert.That(sense0.SelectSingleNode("grammatical-info"), Is.Null);

				var glosses = sense0.SelectNodes("gloss");
				Assert.That(glosses, Is.Not.Null);
				Assert.That(glosses.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "meuwer", "cataeouw" }));

				//Examine the results of the examples
				var examples = sense0.SelectNodes("example");
				Assert.That(examples, Is.Not.Null);
				Assert.That(examples.Count, Is.EqualTo(3));

				//the order of the examples is different since some were removed
				var example1notChanged = examples[0];
				var sourceAttr = example1notChanged.Attributes["source"];
				Assert.That(sourceAttr, Is.Not.Null);
				Assert.That(sourceAttr.Value, Is.EqualTo("reference for second translation"));
				var sentences = example1notChanged.SelectNodes("form");
				Assert.That(sentences, Is.Not.Null);
				Assert.That(sentences.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "Second example sentence.", "Other lang second example." }));
				var translations = example1notChanged.SelectNodes("translation/form");
				Assert.That(translations, Is.Not.Null);
				Assert.That(translations.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "Second example translation" }));
				var note = example1notChanged.SelectSingleNode("note");
				Assert.That(note, Is.Not.Null);
				Assert.That(note.InnerText, Is.EqualTo("reference for second translation"));
				Assert.That(note.Attributes["type"].Value, Is.EquivalentTo("reference"));

				// The original first example was modified so now it appears in the second position
				// since it was first removed and the one in the LiftUpdate file was added.
				var example0WasChanged = examples[1];
				sourceAttr = example0WasChanged.Attributes["source"];
				Assert.That(sourceAttr, Is.Null);
				sentences = example0WasChanged.SelectNodes("form");
				Assert.That(sentences, Is.Not.Null);
				Assert.That(sentences.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "ExampleSentence ",  "Another ws example sentence-CHANGED" }));
				translations = example0WasChanged.SelectNodes("translation/form");
				Assert.That(translations, Is.Not.Null);
				Assert.That(translations.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "This is a translation of example sentences-CHANGED",
						"In another ws this is a translation of exSentences"
					}));
				note = example0WasChanged.SelectSingleNode("note");
				Assert.That(note, Is.Null);

				var exampleWasAdded = examples[2];
				sourceAttr = exampleWasAdded.Attributes["source"];
				Assert.That(sourceAttr, Is.Null);
				sentences = exampleWasAdded.SelectNodes("form");
				Assert.That(sentences, Is.Not.Null);
				Assert.That(sentences.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "Third example sentence.", "Third other lang example." }));
				translations = exampleWasAdded.SelectNodes("translation/form");
				Assert.IsNotNull(translations);
				Assert.That(translations, Is.Not.Null);
				Assert.That(translations.Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
					Is.EqualTo(new[] { "Third example translation" }));
				note = exampleWasAdded.SelectSingleNode("note");
				Assert.That(note, Is.Null);
			}
		}

		[Test]
		public void Examples_AddExamplesToEntryWithoutAny()
		{
			var liftUpdateAddExamples = new[]
			{
				//entry 6
				"<entry dateCreated=\"2012-05-09T06:51:52Z\" dateModified=\"2012-05-09T06:52:16Z\" id=\"tail_98c54484-08a6-4136-abab-b936ddc6ad25\" guid=\"98c54484-08a6-4136-abab-b936ddc6ad25\">",
				"<lexical-unit>",
				"<form lang=\"fr\"><text>tail</text></form>",
				"</lexical-unit>",
				"<sense id=\"6d20a75d-0c74-432e-a169-7042fcd6f026\">",
				"<grammatical-info value=\"Noun\">",
				"</grammatical-info>",
				"<gloss lang=\"en\"><text>wagger</text></gloss>",
					"<example>",
					"<form lang=\"fr\"><text>Example sentence 1</text></form>",
					"<form lang=\"frc\"><text>Another ws example sentence</text></form>",
						"<translation>",
						"<form lang=\"en\"><text>This is a translation of example sentences</text></form>",
						"</translation>",
					"</example>",

					//The LfSynchronicMerger should not replace this second example.
					"<example>",
					"<form lang=\"fr\"><text>OneExampleSentence</text></form>",
						//"<translation>",                                                        //no translation element
						//"<form lang=\"en\"><text>Second example translation</text></form>",
						//"</translation>",
					"</example>",
				"<relation type=\"Whole\" ref=\"1b33697f-91e1-4b57-bab7-824b74d04f86\"/>",
				"</sense>",
				"</entry>"
			};

			using (var env = new TestEnvironment())
			{
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateAddExamples,
									 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				var numberOfEntries = doc.SelectNodes("//entry").Count;
				Assert.AreEqual(8, numberOfEntries);

				XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='tail_98c54484-08a6-4136-abab-b936ddc6ad25']");
				Assert.IsNotNull(changedEntries);
				Assert.AreEqual(1, changedEntries.Count);
				XmlNode changedEntry = changedEntries[0];

				XmlNodeList senses = changedEntry.SelectNodes("sense");
				Assert.IsNotNull(senses);
				Assert.AreEqual(1, senses.Count);
				XmlNode sense0 = senses[0];

				//Examine the results of the examples
				XmlNodeList examples = sense0.SelectNodes("example");
				Assert.IsNotNull(examples);
				Assert.AreEqual(2, examples.Count);

				//the order of the examples is different since some were removed
				XmlNode example0 = examples[0];
				XmlAttribute sourceAttr = example0.Attributes["source"];
				Assert.IsNull(sourceAttr);
				XmlNodeList sentences = example0.SelectNodes("form");
				Assert.IsNotNull(sentences);
				Assert.AreEqual("Example sentence 1", sentences[0].InnerText);
				Assert.AreEqual("Another ws example sentence", sentences[1].InnerText);
				XmlNodeList translations = example0.SelectNodes("translation/form");
				Assert.IsNotNull(translations);
				Assert.AreEqual(1, translations.Count);
				Assert.AreEqual("This is a translation of example sentences", translations[0].InnerText);

				//The original first example was modified so now it appears in the second position since it was first removed
				//and the one in the LiftUpdate file was added.
				XmlNode example1 = examples[1];
				sentences = example1.SelectNodes("form");
				Assert.IsNotNull(sentences);
				Assert.AreEqual(1, sentences.Count);
				Assert.AreEqual("OneExampleSentence", sentences[0].InnerText);
				XmlNode translationNode = example1.SelectSingleNode("translation");
				Assert.IsNull(translationNode);
			}

		}

		[Test]
		public void Examples_RemoveAllExamples()
		{
			string[] liftUpdateRemoveExamples = new[]
			{
				//entry 5
				"<entry dateCreated=\"2012-04-23T16:50:51Z\" dateModified=\"2012-05-09T06:54:13Z\" id=\"cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8\" guid=\"8338bdd5-c1c2-46b2-93d1-2328cbb749c8\">",
				"<lexical-unit>",
				"<form lang=\"fr\"><text>cat</text></form>",
				"</lexical-unit>",
				"<sense id=\"9aaf4b46-f2b5-452f-981f-8517e64e6dc2\">",
					"<gloss lang=\"en\"><text>meuwer</text></gloss>",
					"<gloss lang=\"es\"><text>cataeouw</text></gloss>",

				"</sense>",
				"</entry>"
			};
			using (var env = new TestEnvironment())
			{
				// Setup
				env.CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
				env.CreateLiftUpdateFile(liftUpdateRemoveExamples,
					"LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(LiftFilePath);

				// Exercise
				_merger.MergeUpdatesIntoFile(LiftFilePath, files);

				// Verify
				var doc = GetResult();
				Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(2));
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(8));

				var changedEntries = doc.SelectNodes(
					"//entry[@id='cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8']");
				Assert.That(changedEntries, Has.Count.EqualTo(1));
				XmlNode changedEntry = changedEntries[0];

				var senses = changedEntry.SelectNodes("sense");
				Assert.That(senses, Has.Count.EqualTo(1));
				var sense0 = senses[0];

				Assert.That(sense0.Attributes["id"].Value,
					Is.EqualTo("9aaf4b46-f2b5-452f-981f-8517e64e6dc2"));
				Assert.That(sense0.SelectNodes("example"), Has.Count.EqualTo(0));
			}
		}

		[Test]
		public void NewEntriesAdded_MultipleFilesSucessiveChanges()
		{
			const string liftData1 = @"
<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>
<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>
<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>
";
			const string liftUpdate1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>
<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>
";
			const string liftUpdate2 = @"
<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				//This test demonstrates that LiftUpdate files are applied in the order they are created when time stamps are used to order the names up the LIFTUpdate files.
				//Notice that the files names of the LIFT update files are purposely created so that the alphabetical ordering does not match the time stamp ordering.

				//Create a LIFT file with 3 entries which will have updates applied to it.
				env.WriteFile(_baseLiftFileName, s_LiftData1, _directory);
				//Create a .lift.update file with three entries.  One to replace the second entry in the original LIFT file.
				//The other two are new and should be appended to the original LIFT file.
				env.WriteFile("LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, _directory);
				//Create a .lift.update file with two entries.  One to replace one of the changes from the first LiftUpdate file and one new entry.
				env.WriteFile("LiftChangeFileA" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

				// Exercise
				_merger.MergeUpdatesIntoFile(LiftFilePath, files);

				// Verify
				var doc = GetResult();
				Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(2));
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(6));

				Assert.That(doc.SelectNodes("//entry[@id='one']"), Has.Count.EqualTo(1));
				Assert.That(doc.SelectNodes("//entry[@id='two']"), Has.Count.EqualTo(0));
				Assert.That(doc.SelectNodes("//entry[@id='twoblatblat']"), Has.Count.EqualTo(1));
				Assert.That(doc.SelectNodes("//entry[@id='four']"), Has.Count.EqualTo(0));
				Assert.That(doc.SelectNodes("//entry[@id='fourChangedFirstAddition']"), Has.Count.EqualTo(1));
				Assert.That(doc.SelectNodes("//entry[@id='six']"), Has.Count.EqualTo(1));
			}

		}

		[Test]
		public void EntryDeleted_DeletionDateAdded()
		{
			const string liftData1 = @"
<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>
<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>
<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>
";
			const string liftUpdateDeleteEntry = @"
<entry id='two' dateCreated='2012-05-04T04:19:57Z' dateModified='2012-05-04T04:19:57Z' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22' dateDeleted='2012-05-08T06:40:44Z'></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				// This test demonstrates that a deletion of an entry is applied to a LIFT file.
				// Now 'tomb stoning' is done.  The entry is not actually deleted, but a dateDeleted
				// attribute is added

				// Create a LIFT file with 3 entries which will have updates applied to it.
				env.WriteFile(_baseLiftFileName, liftData1, _directory);
				// Create a .lift.update file with and entry which is indicating that an entry was
				// deleted (tombstone).
				env.WriteFile("LiftChangeFile" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUpdateDeleteEntry, _directory);
				var files = SynchronicMerger.GetPendingUpdateFiles(LiftFilePath);
				var doc = MergeAndGetResult(true, _directory, files);
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(4));
				Assert.That(doc.SelectNodes("//entry[@id='one']"), Has.Count.EqualTo(1));
				var nodesDeleted = doc.SelectNodes("//entry[@id='two' and @guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22']");
				Assert.AreEqual(1, nodesDeleted.Count);   //ensure there is only one entry with this guid
				var nodeDeleted = nodesDeleted[0];
				// Make sure the contents of the node was changed to match the deleted entry from
				// the .lift.update file
				Assert.AreEqual("2012-05-08T06:40:44Z", nodeDeleted.Attributes["dateDeleted"].Value);
				Assert.IsNullOrEmpty(nodeDeleted.InnerXml);
			}

		}

		[Test]
		public void Sha1_ApplyUp1_ApplyUp2()
		{
			const string liftDataSha1 = @"
<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>
<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>SLIGHT CHANGE in .LIFT file</text></form></lexical-unit></entry>
<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>
";
			const string liftUp1ToSha1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'>
<lexical-unit><form lang='nan'><text>ENTRY FOUR adds a lexical unit</text></form></lexical-unit></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			const string liftUp2ToSha1 = @"
<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'>
<lexical-unit><form lang='nan'><text>change ENTRY FOUR again to see if Merge works on same record.</text></form></lexical-unit></entry>
<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>
";
			using (var env = new TestEnvironment())
			{
				//This test demonstrates that a deletion of an entry is applied to a LIFT file.
				//Now 'tomb stoning' is done.  The entry is not actually deleted, but a dateDeleted
				// attribute is added

				//Create a LIFT file with 3 entries which will have updates applied to it.
				env.WriteFile(_baseLiftFileName, liftDataSha1, _directory);
				// Create a .lift.update file with and entry which is indicating that an entry was
				// deleted (tombstone).
				env.WriteFile("LiftUpdate1" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUp1ToSha1, _directory);
				env.WriteFile("LiftUpdate2" + SynchronicMerger.ExtensionOfIncrementalFiles,
					liftUp2ToSha1, _directory);
				FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(
					Path.Combine(_directory, _baseLiftFileName));
				XmlDocument doc = MergeAndGetResult(true, _directory, files);
				Assert.That(doc.SelectNodes("//entry"), Has.Count.EqualTo(5));
				Assert.That(doc.SelectNodes("//entry[@id='one']"), Has.Count.EqualTo(1));
				env.VerifyEntryInnerText(doc, "//entry[@id='one']", "");
				env.VerifyEntryInnerText(doc, "//entry[@id='two']", "SLIGHT CHANGE in .LIFT file");
				env.VerifyEntryInnerText(doc, "//entry[@id='three']", "");
				env.VerifyEntryInnerText(doc, "//entry[@id='six']", "");
				env.VerifyEntryDoesNotExist(doc, "//entry[@id='five']");
				env.VerifyEntryInnerText(doc, "//entry[@id='four']", "change ENTRY FOUR again to see if Merge works on same record.");
			}
		}

		private XmlDocument MergeAndGetResult(bool isBackupFileExpected, string directory, FileInfo[] files)
		{
			_merger.MergeUpdatesIntoFile(LiftFilePath, files);
			ExpectFileCount(isBackupFileExpected ? 2 : 1, directory);

			return GetResult();
		}

		private XmlDocument GetResult()
		{
			var doc = new XmlDocument();
			doc.Load(LiftFilePath);
			Console.WriteLine(File.ReadAllText(LiftFilePath));
			return doc;
		}

		static private void ExpectFileCount(int count, string directory)
		{
			string[] files = Directory.GetFiles(directory);

			var fileList = new StringBuilder();
			foreach (string s in files)
			{
				fileList.Append(s);
				fileList.Append('\n');
			}
			Assert.AreEqual(count, files.Length, fileList.ToString());
		}


	}
}