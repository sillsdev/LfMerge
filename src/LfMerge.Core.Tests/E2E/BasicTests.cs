using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class BasicTests
	{
		[Test]
		public async Task CheckProjectBackupDownloading()
		{
			var env = new SRTestEnvironment();
			await env.Login();
			await env.RollbackProjectToRev("sena-3", -1); // Should make no changes
			// await env.RollbackProjectToRev("sena-3", -2); // Should remove one commit
		}

		[Test]
		public async Task CheckProjectCloning()
		{
			await LcmTestHelper.LexboxLogin("admin", "pass");
			using var sena3 = LcmTestHelper.CloneFromLexbox("sena-3");
			var entries = LcmTestHelper.GetEntries(sena3);
			Console.WriteLine($"Project has {entries.Count()} entries");
			var entry = LcmTestHelper.GetEntry(sena3, new Guid("5db6e79d-de66-4ec6-84c1-af3cd170f90d"));
			var citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("ambuka"));
			LcmTestHelper.SetVernacularText(sena3, entry.CitationForm, "something");
			LcmTestHelper.CommitChanges(sena3, "sena-3");

			using var sena4 = LcmTestHelper.CloneFromLexbox("sena-3", "sena-4");
			entry = LcmTestHelper.GetEntry(sena4, new Guid("5db6e79d-de66-4ec6-84c1-af3cd170f90d"));
			citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("something"));
			LcmTestHelper.UpdateVernacularText(sena4, entry.CitationForm, (s) => $"{s}XYZ");
			citationForm = entry.CitationForm.BestVernacularAlternative.Text;
			Assert.That(citationForm, Is.EqualTo("somethingXYZ"));
			LcmTestHelper.CommitChanges(sena4, "sena-3", "sena-4");
		}
	}
}