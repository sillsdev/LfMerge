using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Palaso.Lift.Merging;
using Palaso.Lift.Validation;
using NUnit.Framework;

namespace lfmergelift.Tests
{
	[TestFixture]
	public class LfSynchronicMergerTests
	{
		private const string _baseLiftFileName = "base.lift";

		private string _directory;
		private LfSynchronicMerger _merger;

		[SetUp]
		public void Setup()
		{
			_merger = new LfSynchronicMerger();
			_directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(_directory);
		}

		[TearDown]
		public void TearDOwn()
		{
			//            DirectoryInfo di = new DirectoryInfo(_directory);
			Directory.Delete(_directory, true);
		}

		private static string GetNextUpdateFileName()
		{
			// Linux filesystem only has resolution of 1 second so we add this to filename so will sort correctly
			return _baseLiftFileName + DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH'-'mm'-'ss'-'FFFFFFF UTC ") + SynchronicMerger.ExtensionOfIncrementalFiles;
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
		public void TestAddSenseAndRemoveSense()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateAddSenseAndRemoveSense,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var howmany = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, doc.SelectNodes("//entry").Count);
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
		private static readonly string[] s_LiftUpdateAddSenseAndRemoveSense = new[]
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


		[Test]
		public void TestDeleteEntry()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateEntryDeleted,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var numberOfEntries = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, doc.SelectNodes("//entry").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='fish_7026c804-799b-4cd2-861f-c8f71cfa9f93']").Count);

			XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='fish_7026c804-799b-4cd2-861f-c8f71cfa9f93']");
			XmlNode deletedNode = changedEntries[0];
			Assert.IsNullOrEmpty(deletedNode.InnerXml);
			var deletionDate = deletedNode.Attributes["dateDeleted"].Value;
			Assert.AreEqual(deletionDate, "2012-05-08T06:40:44Z");
		}

		private static readonly string[] s_LiftUpdateEntryDeleted = new[]
		{
			"<entry dateCreated=\"2012-05-04T03:05:03Z\" dateModified=\"2012-05-04T03:05:50Z\" id=\"fish_7026c804-799b-4cd2-861f-c8f71cfa9f93\" guid=\"7026c804-799b-4cd2-861f-c8f71cfa9f93\" dateDeleted=\"2012-05-08T06:40:44Z\">",
			"</entry>"
		};

		[Test]
		public void TestChangeDateAndLexicalUnit()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateDateAndLexicalUnitChanged,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var numberOfEntries = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, doc.SelectNodes("//entry").Count);

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

		static private readonly string[] s_LiftUpdateDateAndLexicalUnitChanged = new[]
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

			//***traits, custom option list
			//fields,custom  multitext
			//***relation
			//notes
			//
		};

		[Test]
		public void TestChangePOS_Gloss_Definition()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdatePOS_Gloss_Definition_Changed,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var numberOfEntries = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, doc.SelectNodes("//entry").Count);

			XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='hombre_ecfbe958-36a1-4b82-bb69-ca5210355400']");
			Assert.IsNotNull(changedEntries);
			Assert.AreEqual(1, changedEntries.Count);
			XmlNode changedEntry = changedEntries[0];

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

		static private readonly string[] s_LiftUpdatePOS_Gloss_Definition_Changed = new[]
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

			//***traits, custom option list
			//fields,custom  multitext
			//***relation
			//notes
			//
		};


		[Test]
		public void TestChangeOneExampleChanged_AnotherNotChanged_OneAdded()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateChangeExamples,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var numberOfEntries = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, numberOfEntries);

			XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8']");
			Assert.IsNotNull(changedEntries);
			Assert.AreEqual(1, changedEntries.Count);
			XmlNode changedEntry = changedEntries[0];

			XmlNodeList senses = changedEntry.SelectNodes("sense");
			Assert.IsNotNull(senses);
			Assert.AreEqual(1, senses.Count);
			XmlNode sense0 = senses[0];

			var senseId = sense0.Attributes["id"].Value;
			Assert.AreEqual("9aaf4b46-f2b5-452f-981f-8517e64e6dc2", senseId);


			var gramInfo = sense0.SelectSingleNode("grammatical-info");
			Assert.IsNull(gramInfo);

			XmlNodeList glosses = sense0.SelectNodes("gloss");
			Assert.IsNotNull(glosses);
			Assert.AreEqual(2, glosses.Count);
			String glossText = glosses[0].InnerText;
			Assert.AreEqual("meuwer", glossText);
			glossText = glosses[1].InnerText;
			Assert.AreEqual("cataeouw", glossText);

			//Examine the results of the examples
			XmlNodeList examples = sense0.SelectNodes("example");
			Assert.IsNotNull(examples);
			Assert.AreEqual(3, examples.Count);

			//the order of the examples is different since some were removed
			XmlNode example1notChanged = examples[0];
			XmlAttribute sourceAttr = example1notChanged.Attributes["source"];
			Assert.IsNotNull(sourceAttr);
			Assert.AreEqual("reference for second translation", sourceAttr.Value);
			XmlNodeList sentences = example1notChanged.SelectNodes("form");
			Assert.IsNotNull(sentences);
			Assert.AreEqual("Second example sentence.", sentences[0].InnerText);
			Assert.AreEqual("Other lang second example.", sentences[1].InnerText);
			XmlNodeList translations = example1notChanged.SelectNodes("translation/form");
			Assert.IsNotNull(translations);
			Assert.AreEqual(1, translations.Count);
			Assert.AreEqual("Second example translation", translations[0].InnerText);
			XmlNode note = example1notChanged.SelectSingleNode("note");
			Assert.IsNotNull(note);
			Assert.AreEqual("reference for second translation", note.InnerText);
			XmlAttribute referenceType = note.Attributes["type"];
			Assert.AreEqual("reference", referenceType.Value);

			//The original first example was modified so now it appears in the second position since it was first removed
			//and the one in the LiftUpdate file was added.
			XmlNode example0WasChanged = examples[1];
			sourceAttr = example0WasChanged.Attributes["source"];
			Assert.IsNull(sourceAttr);
			sentences = example0WasChanged.SelectNodes("form");
			Assert.IsNotNull(sentences);
			Assert.AreEqual(2, sentences.Count);
			Assert.AreEqual("ExampleSentence ", sentences[0].InnerText);
			Assert.AreEqual("Another ws example sentence-CHANGED", sentences[1].InnerText);
			translations = example0WasChanged.SelectNodes("translation/form");
			Assert.IsNotNull(translations);
			Assert.AreEqual(2, translations.Count);
			Assert.AreEqual("This is a translation of example sentences-CHANGED", translations[0].InnerText);
			Assert.AreEqual("In another ws this is a translation of exSentences", translations[1].InnerText);
			note = example0WasChanged.SelectSingleNode("note");
			Assert.IsNull(note);


			XmlNode exampleWasAdded = examples[2];
			sourceAttr = exampleWasAdded.Attributes["source"];
			Assert.IsNull(sourceAttr);
			sentences = exampleWasAdded.SelectNodes("form");
			Assert.IsNotNull(sentences);
			Assert.AreEqual("Third example sentence.", sentences[0].InnerText);
			Assert.AreEqual("Third other lang example.", sentences[1].InnerText);
			translations = exampleWasAdded.SelectNodes("translation/form");
			Assert.IsNotNull(translations);
			Assert.AreEqual(1, translations.Count);
			Assert.AreEqual("Third example translation", translations[0].InnerText);
			note = exampleWasAdded.SelectSingleNode("note");
			Assert.IsNull(note);
		}
		static private readonly string[] s_LiftUpdateChangeExamples = new[]
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

		[Test]
		public void TestExamples_AddExamplesToEntryWithoutAny()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateAddExamples,
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

		static private readonly string[] s_LiftUpdateAddExamples = new[]
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

		[Test]
		public void TestExamples_RemoveAllExamples()
		{
			CreateLiftInputFile(s_LiftMainFile, _baseLiftFileName, _directory);
			CreateLiftUpdateFile(s_LiftUpdateRemoveExamples,
								 "LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			var numberOfEntries = doc.SelectNodes("//entry").Count;
			Assert.AreEqual(8, numberOfEntries);

			XmlNodeList changedEntries = doc.SelectNodes("//entry[@id='cat_8338bdd5-c1c2-46b2-93d1-2328cbb749c8']");
			Assert.IsNotNull(changedEntries);
			Assert.AreEqual(1, changedEntries.Count);
			XmlNode changedEntry = changedEntries[0];

			XmlNodeList senses = changedEntry.SelectNodes("sense");
			Assert.IsNotNull(senses);
			Assert.AreEqual(1, senses.Count);
			XmlNode sense0 = senses[0];

			var senseId = sense0.Attributes["id"].Value;
			Assert.AreEqual("9aaf4b46-f2b5-452f-981f-8517e64e6dc2", senseId);


			//Examine the results of the examples
			XmlNodeList examples = sense0.SelectNodes("example");
			Assert.IsNotNull(examples);
			Assert.AreEqual(0, examples.Count);
		}

		static private readonly string[] s_LiftUpdateRemoveExamples = new[]
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
		[Test]
		public void TestNewEntriesAdded_MultipleFilesSucessiveChanges()
		{
			//This test demonstrates that LiftUpdate files are applied in the order they are created when time stamps are used to order the names up the LIFTUpdate files.
			//Notice that the files names of the LIFT update files are purposely created so that the alphabetical ordering does not match the time stamp ordering.

			//Create a LIFT file with 3 entries which will have updates applied to it.
			WriteFile(_baseLiftFileName, s_LiftData1, _directory);
			//Create a .lift.update file with three entries.  One to replace the second entry in the original LIFT file.
			//The other two are new and should be appended to the original LIFT file.
			WriteFile("LiftChangeFileB" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, _directory);
			//Create a .lift.update file with two entries.  One to replace one of the changes from the first LiftUpdate file and one new entry.
			WriteFile("LiftChangeFileA" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));

			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			Assert.AreEqual(6, doc.SelectNodes("//entry").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='one']").Count);
			Assert.AreEqual(0, doc.SelectNodes("//entry[@id='two']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='twoblatblat']").Count);
			Assert.AreEqual(0, doc.SelectNodes("//entry[@id='four']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='fourChangedFirstAddition']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='six']").Count);
		}

		private static readonly string s_LiftData1 =
			"<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
			+ "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>"
			+ "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

		private static readonly string s_LiftUpdate1 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>"
			+ "<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>";

		private static readonly string s_LiftUpdate2 =
			"<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		[Test]
		public void TestEntryDeleted_DeletionDate()
		{
			//This test demonstrates that a deletion of an entry is applied to a LIFT file.
			//Now 'tomb stoning' is done.  The entry is not actally

			//Create a LIFT file with 3 entries which will have updates applied to it.
			WriteFile(_baseLiftFileName, s_LiftData1, _directory);
			//Create a .lift.update file with and entry which is indicating that an entry was deleted (tombstone).
			WriteFile("LiftChangeFile" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdateDeleteEntry, _directory);
			FileInfo[] files = SynchronicMerger.GetPendingUpdateFiles(Path.Combine(_directory, _baseLiftFileName));
			XmlDocument doc = MergeAndGetResult(true, _directory, files);
			Assert.AreEqual(4, doc.SelectNodes("//entry").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='one']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='twoDeleted' and @dateDeleted='2012-05-08T06:40:44Z' and @guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22']").Count);
			Assert.AreEqual(0, doc.SelectNodes("//entry[@id='two' and @dateDeleted='2012-05-08T06:40:44Z' and @guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22']").Count);
			Assert.AreEqual(1, doc.SelectNodes("//entry[@id='six']").Count);
		}

		private static readonly string s_LiftUpdateDeleteEntry =
			"<entry id='twoDeleted' dateCreated='2012-05-04T04:19:57Z' dateModified='2012-05-04T04:19:57Z' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22' dateDeleted='2012-05-08T06:40:44Z'></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		private XmlDocument MergeAndGetResult(bool isBackupFileExpected, string directory, FileInfo[] files)
		{
			Merge(directory, files);
			ExpectFileCount(isBackupFileExpected ? 2 : 1, directory);

			return GetResult(directory);
		}

		private static XmlDocument GetResult(string directory)
		{
			XmlDocument doc = new XmlDocument();
			string outputPath = Path.Combine(directory, _baseLiftFileName);
			doc.Load(outputPath);
			Console.WriteLine(File.ReadAllText(outputPath));
			return doc;
		}

		private void Merge(string directory, FileInfo[] files)
		{
			this._merger.MergeUpdatesIntoFile(Path.Combine(directory, _baseLiftFileName), files);
		}

		static private void ExpectFileCount(int count, string directory)
		{
			string[] files = Directory.GetFiles(directory);

			StringBuilder fileList = new StringBuilder();
			foreach (string s in files)
			{
				fileList.Append(s);
				fileList.Append('\n');
			}
			Assert.AreEqual(count, files.Length, fileList.ToString());
		}

		private static void CreateLiftInputFile(IList<string> data, string fileName, string directory)
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
		private static void CreateLiftUpdateFile(IList<string> data, string fileName, string directory)
		{
			string path = Path.Combine(directory, fileName);
			if (File.Exists(path))
				File.Delete(path);
			using (var wrtr = File.CreateText(path))
			{
				wrtr.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
				wrtr.WriteLine("<lift version =\""
							   + Validator.LiftVersion
							   + "\" producer=\"WeSay.1Pt0Alpha\" xmlns:flex=\"http://fieldworks.sil.org\">");
				for (var i = 0; i < data.Count; ++i)
					wrtr.WriteLine(data[i]);
				wrtr.WriteLine("</lift>");
				wrtr.Close();
			}

			//pause so they don't all have the same time
			Thread.Sleep(100);
		}

		static private string WriteFile(string fileName, string xmlForEntries, string directory)
		{
			StreamWriter writer = File.CreateText(Path.Combine(directory, fileName));
			string content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
							 + "<lift version =\""
							 + Validator.LiftVersion
							 + "\" producer=\"WeSay.1Pt0Alpha\" xmlns:flex=\"http://fieldworks.sil.org\">"
							 + xmlForEntries
							 + "</lift>";
			writer.Write(content);
			writer.Close();
			writer.Dispose();

			//pause so they don't all have the same time
			Thread.Sleep(100);

			return content;
		}

		//=================================================================================================================================
		//=================================================================================================================================
		//=================================================================================================================================
		//=================================================================================================================================
		//=================================================================================================================================

		private string ReadFileContentIntoString(string FileName)
		{
			StreamReader sr = null;
			string FileContents = null;

			try
			{
				FileStream fs = new FileStream(FileName, FileMode.Open,
											   FileAccess.Read);
				sr = new StreamReader(fs);
				FileContents = sr.ReadToEnd();
			}
			finally
			{
				if (sr != null)
					sr.Close();
			}

			return FileContents;
		}

		private string FormattedXml3entries
		{
			get
			{
				return
					"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<lift\r\n\tversion=\"0.13\"\r\n\tproducer=\"WeSay.1Pt0Alpha\" xmlns:flex=\"http://fieldworks.sil.org\">\r\n\t<entry\r\n\t\tid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef7\"\r\n\t\tdateCreated=\"2009-10-07T04:07:54Z\"\r\n\t\tdateModified=\"2009-10-07T04:10:34Z\"\r\n\t\tguid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef7\">\r\n\t\t<lexical-unit>\r\n\t\t\t<form\r\n\t\t\t\tlang=\"nan\">\r\n\t\t\t\t<text>test</text>\r\n\t\t\t</form>\r\n\t\t</lexical-unit>\r\n\t\t<sense\r\n\t\t\tid=\"e6f93a8d-7883-4c1c-877e-e34db9f06cdc\">\r\n\t\t\t<definition>\r\n\t\t\t\t<form\r\n\t\t\t\t\tlang=\"en\">\r\n\t\t\t\t\t<text>difficult; slow</text>\r\n\t\t\t\t</form>\r\n\t\t\t</definition>\r\n\t\t</sense>\r\n\t</entry>\r\n\t<entry\r\n\t\tid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef8\"\r\n\t\tdateCreated=\"2009-10-07T04:07:54Z\"\r\n\t\tdateModified=\"2009-10-07T04:10:34Z\"\r\n\t\tguid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef8\">\r\n\t\t<lexical-unit>\r\n\t\t\t<form\r\n\t\t\t\tlang=\"nan\">\r\n\t\t\t\t<text>test</text>\r\n\t\t\t</form>\r\n\t\t</lexical-unit>\r\n\t\t<sense\r\n\t\t\tid=\"e6f93a8d-7883-4c1c-877e-e34db9f06cdd\">\r\n\t\t\t<definition>\r\n\t\t\t\t<form\r\n\t\t\t\t\tlang=\"en\">\r\n\t\t\t\t\t<text>difficult; slow</text>\r\n\t\t\t\t</form>\r\n\t\t\t</definition>\r\n\t\t</sense>\r\n\t</entry>\r\n\t<entry\r\n\t\tid=\"Xtest3_92f0c58a-9ad6-4715-ad9b-d56597217ac3\"\r\n\t\tdateCreated=\"2010-02-04T03:58:33Z\"\r\n\t\tdateModified=\"2010-02-04T03:58:40Z\"\r\n\t\tguid=\"92f0c58a-9ad6-4715-ad9b-d56597217ac3\">\r\n\t\t<lexical-unit>\r\n\t\t\t<form\r\n\t\t\t\tlang=\"v\">\r\n\t\t\t\t<text>Xtest3</text>\r\n\t\t\t</form>\r\n\t\t</lexical-unit>\r\n\t\t<sense\r\n\t\t\tid=\"4bc90f1e-1d4a-418f-a6e0-a4191b3e04f3\">\r\n\t\t\t<definition>\r\n\t\t\t\t<form\r\n\t\t\t\t\tlang=\"en\">\r\n\t\t\t\t\t<text>test3</text>\r\n\t\t\t\t</form>\r\n\t\t\t</definition>\r\n\t\t</sense>\r\n\t</entry>\r\n</lift>";
			}
		}

		private string FormattedXml2entries
		{
			get
			{
				return
					"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<lift\r\n\tversion=\"0.13\"\r\n\tproducer=\"WeSay.1Pt0Alpha\" xmlns:flex=\"http://fieldworks.sil.org\">\r\n\t<entry\r\n\t\tid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef7\"\r\n\t\tdateCreated=\"2009-10-07T04:07:54Z\"\r\n\t\tdateModified=\"2009-10-07T04:10:34Z\"\r\n\t\tguid=\"03d4b59a-9673-4f16-964e-4ea9f0636ef7\">\r\n\t\t<lexical-unit>\r\n\t\t\t<form\r\n\t\t\t\tlang=\"nan\">\r\n\t\t\t\t<text>test</text>\r\n\t\t\t</form>\r\n\t\t</lexical-unit>\r\n\t\t<sense\r\n\t\t\tid=\"e6f93a8d-7883-4c1c-877e-e34db9f06cdc\">\r\n\t\t\t<definition>\r\n\t\t\t\t<form\r\n\t\t\t\t\tlang=\"en\">\r\n\t\t\t\t\t<text>difficult; slow</text>\r\n\t\t\t\t</form>\r\n\t\t\t</definition>\r\n\t\t</sense>\r\n\t</entry>\r\n\t<entry\r\n\t\tid=\"Xtest3_92f0c58a-9ad6-4715-ad9b-d56597217ac3\"\r\n\t\tdateCreated=\"2010-02-04T03:58:33Z\"\r\n\t\tdateModified=\"2010-02-04T03:58:40Z\"\r\n\t\tguid=\"92f0c58a-9ad6-4715-ad9b-d56597217ac3\">\r\n\t\t<lexical-unit>\r\n\t\t\t<form\r\n\t\t\t\tlang=\"v\">\r\n\t\t\t\t<text>Xtest3</text>\r\n\t\t\t</form>\r\n\t\t</lexical-unit>\r\n\t\t<sense\r\n\t\t\tid=\"4bc90f1e-1d4a-418f-a6e0-a4191b3e04f3\">\r\n\t\t\t<definition>\r\n\t\t\t\t<form\r\n\t\t\t\t\tlang=\"en\">\r\n\t\t\t\t\t<text>test3</text>\r\n\t\t\t\t</form>\r\n\t\t\t</definition>\r\n\t\t</sense>\r\n\t</entry>\r\n</lift>";
			}
		}


		private void WriteLongSingleLineBaseFileWithWhiteSpaceAfterEntryOpenElement()
		{
			WriteFile(_baseLiftFileName,
					  @"<entry id=""03d4b59a-9673-4f16-964e-4ea9f0636ef7"" dateCreated=""2009-10-07T04:07:54Z"" dateModified=""2009-10-07T04:10:34Z"" guid=""03d4b59a-9673-4f16-964e-4ea9f0636ef7"">
<lexical-unit><form lang=""nan""><text>test</text></form></lexical-unit><sense id=""e6f93a8d-7883-4c1c-877e-e34db9f06cdc""><definition><form lang=""en""><text>difficult; slow</text></form></definition></sense></entry>", _directory);
		}


	}
}