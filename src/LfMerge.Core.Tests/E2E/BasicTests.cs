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
		}
	}
}
