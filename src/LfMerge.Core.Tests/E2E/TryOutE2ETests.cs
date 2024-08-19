using System;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class TryOutE2ETests : E2ETestBase
	{
		[Test]
		public async Task E2E_LFDataChangedLDDataChanged_LFWins()
		{
			// Setup

			var lfProject = await CreateLfProjectFromSena3();
			var fwProjectCode = Regex.Replace(lfProject.ProjectCode, "^sr-", "fw-");
			var fwProject = CloneFromLexbox(lfProject.ProjectCode, fwProjectCode);

			// Modify FW data first, then push to Lexbox
			Guid entryId = Guid.Parse("0006f482-a078-4cef-9c5a-8bd35b53cf72");
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
