using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Palaso.Lift.Merging;
using Palaso.Progress.LogBox;
using Palaso.TestUtilities;
using Chorus.VcsDrivers.Mercurial;
using NullProgress=Palaso.Progress.LogBox.NullProgress;


namespace lfmergelift.Tests
{
	[TestFixture]
	public class LiftUpdateProcessorTests
	{
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

		private static readonly string s_LiftData1 =
		   "<entry id='one' guid='0ae89610-fc01-4bfd-a0d6-1125b7281dd1'></entry>"
		   + "<entry id='two' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'><lexical-unit><form lang='nan'><text>test</text></form></lexical-unit></entry>"
		   + "<entry id='three' guid='80677C8E-9641-486e-ADA1-9D20ED2F5B69'></entry>";

		private static readonly string s_LiftUpdate1 =
			"<entry id='four' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='twoblatblat' guid='0ae89610-fc01-4bfd-a0d6-1125b7281d22'></entry>"
			+ "<entry id='five' guid='6D2EC48D-C3B5-4812-B130-5551DC4F13B6'></entry>";

		private static readonly string s_LiftUpdate2 =
			"<entry id='fourChangedFirstAddition' guid='6216074D-AD4F-4dae-BE5F-8E5E748EF68A'></entry>"
			+ "<entry id='six' guid='107136D0-5108-4b6b-9846-8590F28937E8'></entry>";


		[Test]
		public void Test_OneProject_TwoUpdateFiles()
		{
			//This test puts 3 files in the mergerWork/liftUpdates folder.  Two are .lift.updates files and one is not.
			//Verify that only the .lift.updates files are returned from the call to GetPendingUpdateFiles
			using (var e = new LangForgeTestEnvironment())
			{
				e.CreateWebWorkProjectFolder("ProjA");
				LfSynchronicMergerTests.WriteFile("ProjA.Lift", s_LiftData1, e.GetProjWebWorkPath("ProjA"));
				var projAWorkRepo = (new HgTestSetup(e.GetProjWebWorkPath("ProjA"))).Repository;
				projAWorkRepo.AddAndCheckinFile();

				e.CreateMergeWorkProjectFolder("ProjA");
				projAWorkRepo.CloneLocal(e.GetProjMergeWorkPath("ProjA"));
				var projAMergeRepo = new HgRepository(e.GetProjMergeWorkPath("ProjA"), new NullProgress());
				////Create a file a LIFT file for a project.

				//HgRepository _hgRepo = new HgRepository();


				//Create a .lift.update file.
				LfSynchronicMergerTests.WriteFile("ProjA_123_extraB" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate1, e.LangForgeDirs.LiftUpdatesPath);
				//Create another .lift.update file
				LfSynchronicMergerTests.WriteFile("ProjA_123_extraA" + SynchronicMerger.ExtensionOfIncrementalFiles, s_LiftUpdate2, e.LangForgeDirs.LiftUpdatesPath);



			}
		}

		[Test]
		public void Test_TwoProjects_TwoUpdateFiles()
		{
			using (var e = new LangForgeTestEnvironment())
			{

			}
		}

		public const string ExtensionOfLiftFiles = ".lift";
		private string LiftFileFullPath(String path, String filename)
		{
			return Path.Combine(path, filename + ExtensionOfLiftFiles);
		}

		private string LiftUpdateFileFullPath(String filename, LangForgeTestEnvironment e)
		{
			return Path.Combine(e.LangForgeDirs.LiftUpdatesPath, filename + SynchronicMerger.ExtensionOfIncrementalFiles);
		}
	}

	class LangForgeTestEnvironment : IDisposable
	{
		private readonly TemporaryFolder _languageForgeServerFolder = new TemporaryFolder("LangForge");
		public String LanguageForgeFolder
		{
			get { return _languageForgeServerFolder.Path; }
		}

		public readonly LangForgeDirectories LangForgeDirs;

		public LangForgeTestEnvironment()
		{
			LangForgeDirs = new LangForgeDirectories(LanguageForgeFolder);
			CreateAllTestFolders();
		}

		private void CreateAllTestFolders()
		{
			CreateWebWorkFolder();
			CreateMergeWorkFolder();
			CreateMergeWorkProjectsFolder();
			CreateLiftUpdatesFolder();
		}

		private void CreateWebWorkFolder()
		{
			Directory.CreateDirectory(LangForgeDirs.WebWorkPath);
		}
		public void CreateWebWorkProjectFolder(String projectName)
		{
			Directory.CreateDirectory(GetProjWebWorkPath(projectName));
		}


		private void CreateMergeWorkFolder()
		{
			Directory.CreateDirectory(LangForgeDirs.MergeWorkPath);
		}
		private void CreateLiftUpdatesFolder()
		{
			Directory.CreateDirectory(LangForgeDirs.LiftUpdatesPath);
		}
		private void CreateMergeWorkProjectsFolder()
		{
			Directory.CreateDirectory(LangForgeDirs.MergeWorkProjects);
		}
		public void CreateMergeWorkProjectFolder(String projectName)
		{
			Directory.CreateDirectory(GetProjMergeWorkPath(projectName));
		}

		public String GetProjWebWorkPath(String projectName)
		{
			return Path.Combine(LangForgeDirs.WebWorkPath, projectName);
		}

		public String GetProjMergeWorkPath(String projectName)
		{
			return Path.Combine(LangForgeDirs.MergeWorkProjects, projectName);
		}


		public void Dispose()
		{
			_languageForgeServerFolder.Dispose();
		}

	}

	public class HgTestSetup
	{
		public HgRepository Repository;
		private Palaso.Progress.LogBox.ConsoleProgress _progress;
		private String _HgRootPath;


		public HgTestSetup(String HgRootPath)
		{
			_HgRootPath = HgRootPath;
			_progress = new ConsoleProgress();
			//HgRepository.CreateRepositoryInExistingDir(_HgRootPath, _progress);
			HgRepository.CreateRepositoryInExistingDir(_HgRootPath, new NullProgress());
			//Repository = new HgRepository(_HgRootPath, new NullProgress() as IProgress);
			Repository = new HgRepository(_HgRootPath, new NullProgress());
		}

	}
}
