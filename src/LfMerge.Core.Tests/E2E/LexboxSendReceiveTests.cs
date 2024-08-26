using System;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class LexboxSendReceiveTests : E2ETestBase
	{
		// This test will often trigger a race condition in LexBox that causes the *next* test to fail
		// when `hg clone` returns 404. This is because of hgweb's directory cache, which only refreshes
		// if it hasn't been refreshed more than N seconds ago (default 20, LexBox currently uses 5).
		// Which means that even though CreateLfProjectFromSena3 has created the LexBox project, LexBox's
		// copy of hgweb doesn't see it yet so it can't be cloned.
		//
		// The solution will be to adjust CloneRepoFromLexbox to take a parameter that is the number of
		// seconds to wait for the project to become visible, and retry 404's until that much time has elapsed.
		// Then only throw an exception if hgweb is still returning 404 after its dir cache should be refreshed.
		[Test]
		public async Task E2E_CheckFwProjectCreation()
		{
			var code = await CreateEmptyFlexProjectInLexbox();
			Console.WriteLine($"Created new project {code}");
		}

		[Test]
		public async Task E2E_LFDataChangedLDDataChanged_LFWins()
		{
			// Setup

			var lfProject = await CreateLfProjectFromSena3();
			var fwProjectCode = Regex.Replace(lfProject.ProjectCode, "^sr-", "fw-");
			var fwProject = CloneFwProjectFromLexbox(lfProject.ProjectCode, fwProjectCode);

			// Modify FW data first, then push to Lexbox
			Guid entryId = LcmTestHelper.GetFirstEntry(fwProject).Guid;
			var (unchangedGloss, origFwDateModified, fwDateModified) = UpdateFwGloss(fwProject, entryId, text => text + " - changed in FW");
			CommitAndPush(fwProject, lfProject.ProjectCode, fwProjectCode, "Modified gloss in FW");

			// Modify LF data second
			var (_, origLfDateModified, _) = UpdateLfGloss(lfProject, entryId, "pt", text => text + " - changed in LF");

			// Exercise

			SendReceiveToLexbox(lfProject);

			// Verify

			// LF side should win conflict since its modified date was later
			var lfEntryAfterSR = _mongoConnection.GetLfLexEntryByGuid(entryId);
			Assert.That(lfEntryAfterSR?.Senses?[0]?.Gloss?["pt"]?.Value, Is.EqualTo(unchangedGloss + " - changed in LF"));
			// LF's modified dates should have been updated by the sync action
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(origLfDateModified));
			// Remember that FieldWorks's DateModified is stored in local time for some incomprehensible reason...
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(fwDateModified.ToUniversalTime()));
		}
	}
}
