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
			var sena3 = LcmTestHelper.CloneFromLexbox("sena-3");
			var entries = LcmTestHelper.GetEntries(sena3);
			Console.WriteLine($"Project has {entries.Count()} entries");
			sena3.Dispose();
		}
	}
}
