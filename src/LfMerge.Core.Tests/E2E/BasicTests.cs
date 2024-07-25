using System;
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
	}
}
