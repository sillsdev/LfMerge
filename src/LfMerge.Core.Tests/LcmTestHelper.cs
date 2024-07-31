using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.FieldWorks;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;
using SIL.PlatformUtilities;
using SIL.Progress;

namespace LfMerge.Core.Tests
{
	public static class LcmTestHelper
	{
		public static string HgCommand =>
			Path.Combine(TestEnvironment.FindGitRepoRoot(), "Mercurial",
				Platform.IsWindows ? "hg.exe" : "hg");

		public static string LexboxHostname = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname) ?? "localhost";
		public static string LexboxProtocol = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol) ?? "http";
		public static string LexboxPort = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriPort) ?? (LexboxProtocol == "http" ? "80" : "443");
		public static Uri LexboxUrl = new Uri($"{LexboxProtocol}://{LexboxHostname}:{LexboxPort}");

		public static string BaseDir = Path.Combine(Path.GetTempPath(), nameof(LcmTestHelper));

		public static HttpClientHandler Handler { get; set; } = new HttpClientHandler();
		public static CookieContainer Cookies => Handler.CookieContainer;
		public static HttpClient Http { get; set; } = new HttpClient(Handler);
		public static IProgress NullProgress = new NullProgress();

		public static async Task<string> LexboxLogin(string username, string password)
		{
			await Http.PostAsJsonAsync(new Uri(LexboxUrl, "/api/login"), new { EmailOrUsername=username, Password=password });
			var cookies = Cookies.GetCookies(LexboxUrl);
			return cookies[".LexBoxAuth"].Value;
		}

		public static FwProject CloneFromLexbox(string code, string? newCode = null)
		{
			var projUrl = new Uri(LexboxUrl, $"/hg/{code}");
			var withAuth = new UriBuilder(projUrl) { UserName = "admin", Password = "pass" };
			newCode ??= code;
			var dest = Path.Combine(BaseDir, "webwork", newCode);
			MercurialTestHelper.CloneRepo(withAuth.Uri.AbsoluteUri, dest);
			var fwdataPath = Path.Join(dest, $"{newCode}.fwdata");
			var progress = new NullProgress();
			MercurialTestHelper.ChangeBranch(dest, "tip");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(progress, false, fwdataPath);
			var settings = new LfMergeSettingsDouble(BaseDir);
			return new FwProject(settings, newCode);
		}

		public static void CommitChanges(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			localCode ??= code;
			var projUrl = new Uri(LexboxUrl, $"/hg/{code}");
			var withAuth = new UriBuilder(projUrl) { UserName = "admin", Password = "pass" };
			if (!project.IsDisposed) project.Dispose();
			commitMsg ??= "Auto-commit";
			var projectDir = Path.Combine(BaseDir, "webwork", localCode);
			var fwdataPath = Path.Join(projectDir, $"{localCode}.fwdata");
			LfMergeBridge.LfMergeBridge.DisassembleFwdataFile(NullProgress, false, fwdataPath);
			MercurialTestHelper.HgCommit(projectDir, commitMsg);
			MercurialTestHelper.HgPush(projectDir, withAuth.Uri.AbsoluteUri);
		}

		public static IEnumerable<ILexEntry> GetEntries(FwProject project)
		{
			return project?.ServiceLocator?.LanguageProject?.LexDbOA?.Entries ?? [];
		}

		public static ILexEntry GetEntry(FwProject project, Guid guid)
		{
			var repo = project?.ServiceLocator?.GetInstance<ILexEntryRepository>();
			return repo.GetObject(guid);
		}

		public static void SetVernacularText(FwProject project, IMultiUnicode field, string newText)
		{
			var accessor = project.Cache.ActionHandlerAccessor;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", accessor, () => {
				field.SetVernacularDefaultWritingSystem(newText);
			});
		}

		public static void SetAnalysisText(FwProject project, IMultiUnicode field, string newText)
		{
			var accessor = project.Cache.ActionHandlerAccessor;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", accessor, () => {
				field.SetAnalysisDefaultWritingSystem(newText);
			});
		}

		public static void UpdateVernacularText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestVernacularAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetVernacularText(project, field, newText);
			}
		}

		public static void UpdateAnalysisText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestAnalysisAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetAnalysisText(project, field, newText);
			}
		}
	}
}
