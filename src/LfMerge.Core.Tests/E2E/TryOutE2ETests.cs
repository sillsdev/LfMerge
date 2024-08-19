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

			var fwEntry = LcmTestHelper.GetEntry(fwProject, entryId);
			Assert.That(fwEntry, Is.Not.Null);
			string unchangedGloss = LcmTestHelper.UpdateAnalysisText(fwProject, fwEntry.SensesOS[0].Gloss, text => text + " - changed in FW");
			DateTime fwDateModified = fwEntry.DateModified;
			CommitAndPush(fwProject, lfProject.ProjectCode, fwProjectCode, "Modified gloss in FW");

			// Modify LF data second, then launch Send/Receive

			var lfEntry = _mongoConnection.GetLfLexEntryByGuid(entryId);
			// Verify LF entry not yet affected by FW change because Send/Receive has not happened yet
			Assert.That(lfEntry.Senses[0].Gloss["pt"].Value, Is.EqualTo(unchangedGloss));
			// Capture original modified dates so we can check later that they've been updated
			DateTime originalLfDateModified = lfEntry.DateModified;
			DateTime originalLfAuthorInfoModifiedDate = lfEntry.AuthorInfo.ModifiedDate;

			lfEntry.Senses[0].Gloss["pt"].Value = unchangedGloss + " - changed in LF";
			lfEntry.AuthorInfo.ModifiedDate = lfEntry.DateModified = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(lfProject, lfEntry);

			// Ensure LF project will S/R to local LexBox

			var saveEnv = Environment.GetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri);
			Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri, SRTestEnvironment.LexboxUrlForProjectWithAuth(lfProject.ProjectCode).AbsoluteUri);
			Console.WriteLine(SRTestEnvironment.LexboxUrlForProjectWithAuth(lfProject.ProjectCode).AbsoluteUri);

			// Exercise

			// Do LfMerge Send/Receive of LF project]
			var lfEntryBeforeSR = _mongoConnection.GetLfLexEntryByGuid(entryId);
			Assert.That(lfEntryBeforeSR?.Senses?[0]?.Gloss?["pt"]?.Value, Is.EqualTo(unchangedGloss + " - changed in LF"));

			try {
				var syncAction = new SynchronizeAction(TestEnv.Settings, TestEnv.Logger);
				syncAction.Run(lfProject);
			} finally {
				Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_LanguageDepotRepoUri, saveEnv);
			}

			// Verify

			// Verify that the result of the conflict was LF winning, and LF's modified dates got updated by the sync action

			var lfEntryAfterSR = _mongoConnection.GetLfLexEntryByGuid(entryId);
			Assert.That(lfEntryAfterSR?.Senses?[0]?.Gloss?["pt"]?.Value, Is.EqualTo(unchangedGloss + " - changed in LF"));
			Assert.That(lfEntryAfterSR.DateModified, Is.GreaterThan(originalLfDateModified));
			Assert.That(lfEntryAfterSR.DateModified, Is.GreaterThan(fwDateModified));
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(originalLfAuthorInfoModifiedDate));
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(fwDateModified));
		}
	}
}
