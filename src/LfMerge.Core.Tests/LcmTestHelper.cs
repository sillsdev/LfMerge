using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.FieldWorks;
using SIL.LCModel;
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

		public static async Task<string> LexboxLogin(string username, string password)
		{
			await Http.PostAsJsonAsync(new Uri(LexboxUrl, "/api/login"), new { EmailOrUsername=username, Password=password });
			var cookies = Cookies.GetCookies(LexboxUrl);
			return cookies[".LexBoxAuth"].Value;
		}

		public static FwProject CloneFromLexbox(string code, string? dest = null)
		{
			var projUrl = new Uri(LexboxUrl, $"/hg/{code}");
			var withAuth = new UriBuilder(projUrl) { UserName = "admin", Password = "pass" };
			dest ??= Path.Combine(BaseDir, "webwork", code);
			MercurialTestHelper.CloneRepo(withAuth.Uri.AbsoluteUri, dest);
			var fwdataPath = Path.Join(dest, $"{code}.fwdata");
			var progress = new NullProgress();
			MercurialTestHelper.ChangeBranch(dest, "tip");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(progress, false, fwdataPath);
			var settings = new LfMergeSettingsDouble(BaseDir);
			return new FwProject(settings, code);
		}

		public static IEnumerable<ILexEntry> GetEntries(FwProject project)
		{
			return project?.ServiceLocator?.LanguageProject?.LexDbOA?.Entries ?? [];
		}
	}
}
