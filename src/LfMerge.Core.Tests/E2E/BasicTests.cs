using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LfMerge.Core.DataConverters;
using LfMergeBridge.LfMergeModel;
using NUnit.Framework;
using SIL.Progress;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class BasicTests : SRTestBase
	{
		[Test]
		public async Task CheckProjectBackupDownloading()
		{
			await TestEnv.RollbackProjectToRev("sena-3", -1); // Should make no changes
			// await TestEnv.RollbackProjectToRev("sena-3", -2); // Should remove one commit
		}

		[Test]
		[Property("projectCode", "sena-3")] // This will cause it to auto-reset the project afterwards
		public async Task CheckProjectCloning()
		{
			using var sena3 = CloneFromLexbox("sena-3");
			var entryCount = LcmTestHelper.CountEntries(sena3);
			Assert.That(entryCount, Is.EqualTo(1462));
			var entry = LcmTestHelper.GetEntry(sena3, new Guid("5db6e79d-de66-4ec6-84c1-af3cd170f90d"));
			Assert.That(entry, Is.Not.Null);
			var citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("ambuka"));
			LcmTestHelper.SetVernacularText(sena3, entry.CitationForm, "something");
			CommitAndPush(sena3, "sena-3");

			using var sena4 = CloneFromLexbox("sena-3", "sena-4");
			entry = LcmTestHelper.GetEntry(sena4, new Guid("5db6e79d-de66-4ec6-84c1-af3cd170f90d"));
			citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("something"));
			LcmTestHelper.UpdateVernacularText(sena4, entry.CitationForm, (s) => $"{s}XYZ");
			citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("somethingXYZ"));
			CommitAndPush(sena4, "sena-3", "sena-4");
		}

		[Test]
		[Property("projectCode", "sena-3")]
		public async Task SendReceiveComments()
		{
			using var sena3 = CloneFromLexbox("sena-3");
			var entryCount = LcmTestHelper.CountEntries(sena3);
			Assert.That(entryCount, Is.EqualTo(1462));
			var entry = LcmTestHelper.GetEntry(sena3, new Guid("5db6e79d-de66-4ec6-84c1-af3cd170f90d"));
			Assert.That(entry, Is.Not.Null);
			var comment = new LfComment {
				Guid = new Guid("6864b40d-6ad8-4c42-9590-114c0b8495c8"),
				Content = "Comment for test",
				// Let's see if that's enough
			};
			var comments = new List<KeyValuePair<string, LfComment>> { new KeyValuePair<string, LfComment>("6864b40d-6ad8-4c42-9590-114c0b8495c8", comment) };
			var result = ConvertMongoToLcmComments.CallLfMergeBridge(comments, out var lfmergeBridgeOutput, FwDataPathForProject("sena-3"), new NullProgress(), TestEnv.Logger);
			Console.WriteLine(lfmergeBridgeOutput);
			CommitAndPush(sena3, "sena-3");
		}

		[Test]
		public async Task UploadNewProject()
		{
			var testCode = await CreateNewProjectFromSena3();
			Console.WriteLine($"Created {testCode}");
		}
	}
}
