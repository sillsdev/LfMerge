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

			var (unchangedGloss, origFwDateModified, fwDateModified) = UpdateFwGloss(fwProject, entryId, text => text + " - changed in FW");
			CommitAndPush(fwProject, lfProject.ProjectCode, fwProjectCode, "Modified gloss in FW");

			// Modify LF data second

			var (_, origLfDateModified, _) = UpdateLfGloss(lfProject, entryId, "pt", text => text + " - changed in LF");

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
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(origLfDateModified));
			// Remember that FieldWorks's DateModified is stored in local time for some incomprehensible reason...
			Assert.That(lfEntryAfterSR.AuthorInfo.ModifiedDate, Is.GreaterThan(fwDateModified.ToUniversalTime()));
		}
	}
}
