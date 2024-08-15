using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LfMerge.Core.DataConverters;
using LfMergeBridge.LfMergeModel;
using NUnit.Framework;
using SIL.Progress;

using System.IO;
using System.Linq;
using LfMerge.Core.Actions;
using LfMerge.Core.LanguageForge.Model;
using SIL.LCModel;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class TryOutE2ETests : E2ETestBase
	{

		// TODO: Duplicate the test below using LexBox instead of a mock LanguageDepot server

		[Test]
		public async Task E2E_LFDataChangedLDDataChanged_LFWins()
		{
			// Setup

			var lfProject = await CreateLfProjectFromSena3();

			// Do some initial checks to make sure we got the right data

			Guid entryId = Guid.Parse("0006f482-a078-4cef-9c5a-8bd35b53cf72");
			var lcmObject = lfProject.FieldWorksProject?.ServiceLocator?.ObjectRepository?.GetObject(entryId);
			Assert.That(lcmObject, Is.Not.Null);
			var lcmEntry = lcmObject as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.CitationForm.BestVernacularAlternative.Text, Is.EqualTo("cibubu"));

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == entryId);

			Assert.That(lfEntry.CitationForm.BestString(["seh"]), Is.EqualTo("cibubu"));

			// TODO: Finish implementing the rest of the original test (below) from SynchronizeActionTests, then create helper methods for reusable parts

			// Capture original modified dates from the particular entry we're interested in

			// IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			// LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			// DateTime originalLfDateModified = lfEntry.DateModified;
			// DateTime originalLfAuthorInfoModifiedDate = lfEntry.AuthorInfo.ModifiedDate;

			// Modify the entry in LF, but not yet in LD

			// string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			// string fwChangedGloss = unchangedGloss + " - changed in FW";
			// string lfChangedGloss = unchangedGloss + " - changed in LF";
			// lfEntry.Senses[0].Gloss["en"].Value = lfChangedGloss;
			// lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			// _mongoConnection.UpdateRecord(_lfProject, lfEntry);

			// Verify that the LD project has the FW value for the gloss

			// _lDProject = new LanguageDepotMock(testProjectCode, _lDSettings);
			// var lDcache = _lDProject.FieldWorksProject.Cache;
			// var lDLcmEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			// Assert.That(lDLcmEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(fwChangedGloss));

			// Capture the original modified date from the entry in the FW/LCM project

			// DateTime originalLdDateModified = lDLcmEntry.DateModified;

			// Exercise

			// Capture date/time immediately before running, to make sure that SyncAction updates it

			// var sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
			// var timeBeforeRun = DateTime.UtcNow;

			// Actually do the LfMerge Send/Receive with mock LD

			// sutSynchronize.Run(_lfProject);

			// Verify

			// Verify that the result of the conflict was LF winning

			// Assert.That(GetGlossFromMongoDb(_testEntryGuid), Is.EqualTo(lfChangedGloss));

			// Verify that the modified dates got updated when LF won the merge conflict, even though LF's view of the data didn't change

			// LfLexEntry updatedLfEntry = _mongoConnection.GetLfLexEntries().First(e => e.Guid == _testEntryGuid);
			// DateTime updatedLfDateModified = updatedLfEntry.DateModified;
			// DateTime updatedLfAuthorInfoModifiedDate = updatedLfEntry.AuthorInfo.ModifiedDate;
			// // LF had the same data previously; however it's a merge conflict so DateModified
			// // got updated
			// Assert.That(updatedLfDateModified, Is.GreaterThan(originalLfDateModified));
			// // But the LCM modified date (AuthorInfo.ModifiedDate in LF) should be updated.
			// Assert.That(updatedLfAuthorInfoModifiedDate, Is.GreaterThan(originalLfAuthorInfoModifiedDate));
			// Assert.That(updatedLfDateModified, Is.GreaterThan(originalLdDateModified));
			// Assert.That(_mongoConnection.GetLastSyncedDate(_lfProject), Is.GreaterThanOrEqualTo(timeBeforeRun));
		}
	}
}
