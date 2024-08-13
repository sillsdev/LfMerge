using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using NUnit.Framework;
using SIL.LCModel;
using SIL.TestUtilities;
using TusDotNetClient;

namespace LfMerge.Core.Tests
{
	/// <summary>
	/// Test environment for end-to-end testing, i.e. Send/Receive with a real LexBox instance
	/// </summary>
	public class SRTestEnvironment
	{
		public ILogger Logger => MainClass.Logger;
		public Uri LexboxUrl { get; init; }
		public Uri LexboxUrlBasicAuth { get; init; }
		private string? _lexboxHostname;
		public string LexboxHostname => _lexboxHostname ?? Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname) ?? "localhost";
		private string? _lexboxProtocol;
		public string LexboxProtocol => _lexboxProtocol ?? Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol) ?? "http";
		private string? _lexboxPort;
		public string LexboxPort => _lexboxPort ?? (LexboxProtocol == "http" ? "80" : "443");
		private string? _lexboxUsername;
		public string LexboxUsername => _lexboxUsername ?? Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername) ?? "admin";
		private string? _lexboxPassword;
		public string LexboxPassword => _lexboxPassword ?? Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken) ?? "pass";
		private TemporaryFolder TempFolder { get; init; }
		private HttpClient Http { get; init; }
		private HttpClientHandler Handler { get; init; } = new HttpClientHandler();
		private CookieContainer Cookies { get; init; } = new CookieContainer();
		public static SIL.Progress.IProgress NullProgress = new SIL.Progress.NullProgress();
		private string Jwt { get; set; }

		public SRTestEnvironment(string? lexboxHostname = null, string? lexboxProtocol = null, string? lexboxPort = null, string? lexboxUsername = null, string? lexboxPassword = null)
		{
			_lexboxHostname = lexboxHostname;
			_lexboxProtocol = lexboxProtocol;
			_lexboxPort = lexboxPort;
			_lexboxUsername = lexboxUsername;
			_lexboxPassword = lexboxPassword;
			if (lexboxHostname is not null) Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname, lexboxHostname);
			if (lexboxHostname is not null) Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPrivateHostname, lexboxHostname);
			if (lexboxProtocol is not null) Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol, lexboxProtocol);
			if (lexboxUsername is not null) Environment.SetEnvironmentVariable(MagicStrings.EnvVar_HgUsername, lexboxUsername);
			if (lexboxPassword is not null) Environment.SetEnvironmentVariable(MagicStrings.EnvVar_TrustToken, lexboxPassword);
			LexboxUrl = new Uri($"{LexboxProtocol}://{LexboxHostname}:{LexboxPort}");
			LexboxUrlBasicAuth = new Uri($"{LexboxProtocol}://{WebUtility.UrlEncode(LexboxUsername)}:{WebUtility.UrlEncode(LexboxPassword)}@{LexboxHostname}:{LexboxPort}");
			TempFolder = new TemporaryFolder(TestName + Path.GetRandomFileName());
			Handler.CookieContainer = Cookies;
			Http = new HttpClient(Handler);
		}

		public Task Login()
		{
			return LoginAs(LexboxUsername, LexboxPassword);
		}

		public async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			var loginResult = await Http.PostAsync(new Uri(LexboxUrl, "api/login"), JsonContent.Create(new { EmailOrUsername=lexboxUsername, Password=lexboxPassword }));
			var cookies = Cookies.GetCookies(LexboxUrl);
			Jwt = cookies[".LexBoxAuth"].Value;
			// Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
			// Bearer auth on LexBox requires logging in to LexBox via their OAuth flow. For now we'll let the cookie container handle it.
		}

		public Uri LexboxUrlForProject(string code) => new Uri(LexboxUrl, $"hg/{code}");
		public Uri LexboxUrlForProjectWithAuth(string code) => new Uri(LexboxUrlBasicAuth, $"hg/{code}");

		public void InitRepo(string code, string dest)
		{
			var sourceUrl = LexboxUrlForProjectWithAuth(code);
			MercurialTestHelper.CloneRepo(sourceUrl.AbsoluteUri, dest);
		}

		public void InitRepo(string code) => InitRepo(code, Path.Join(TempFolder.Path, code));

		public async Task ResetAndUploadZip(string code, string zipPath)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			await UploadZip(code, zipPath);
		}

		public async Task ResetToEmpty(string code)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			var finishResetUrl = new Uri(LexboxUrl, $"api/project/finishResetProject/{code}");
			await Http.PostAsync(finishResetUrl, null);
		}

		public async Task UploadZip(string code, string zipPath)
		{
			var sourceUrl = new Uri(LexboxUrl, $"/api/project/upload-zip/{code}");
			var file = new FileInfo(zipPath);
			var client = new TusClient();
			// client.AdditionalHeaders["Authorization"] = $"Bearer {Jwt}"; // Once we set up for LexBox OAuth, we'll use Bearer auth instead
			var cookies = Cookies.GetCookies(LexboxUrl);
			var authCookie = cookies[".LexBoxAuth"].ToString();
			client.AdditionalHeaders["cookie"] = authCookie;
			var fileUrl = await client.CreateAsync(sourceUrl.AbsoluteUri, file.Length, ("filetype", "application/zip"));
			await client.UploadAsync(fileUrl, file);
		}

		public async Task DownloadProjectBackup(string code)
		{
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var filename = result.Content.Headers.ContentDisposition?.FileName;
			var savePath = Path.Join(TempFolder.Path, filename);
			using (var outStream = File.Create(savePath))
			{
				await result.Content.CopyToAsync(outStream);
			}
		}

		public async Task RollbackProjectToRev(string code, string rev)
		{
			// Negative rev numbers will be interpreted as Mercurial does: -1 is the tip revision, -2 is one back from the tip, etc.
			// I.e. rolling back to rev -2 will remove the most recent commit
			if (rev == "-1") return; // Already at tip, nothing to do
			var currentTip = await GetTipRev(code);
			if (rev == currentTip) return; // Already at tip, nothing to do
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var zipStream = await result.Content.ReadAsStreamAsync();
			var projectDir = TempFolder.GetPathForNewTempFile(false);
			ZipFile.ExtractToDirectory(zipStream, projectDir);
			var clonedDir = TempFolder.GetPathForNewTempFile(false);
			MercurialTestHelper.CloneRepoAtRev(projectDir, clonedDir, rev);
			var zipPath = TempFolder.GetPathForNewTempFile(false);
			ZipFile.CreateFromDirectory(clonedDir, zipPath);
			await ResetAndUploadZip(code, zipPath);
		}

		public Task RollbackProjectToRev(string code, int revnum)
		{
			return RollbackProjectToRev(code, revnum.ToString());
		}

		public record TipJson(string Node);

		public async Task<string> GetTipRev(string code)
		{
			var tipUrl = new Uri(LexboxUrl, $"/hg/{code}/file/tip?style=json");
			var result = await Http.GetFromJsonAsync<TipJson>(tipUrl);
			return result.Node;
		}

		public void CommitAndPush(FwProject project, string code, string baseDir, string? localCode = null, string? commitMsg = null)
		{
			localCode ??= code;
			var projUrl = new Uri(LexboxUrl, $"/hg/{code}");
			var withAuth = new UriBuilder(projUrl) { UserName = "admin", Password = "pass" };
			project.Cache.ActionHandlerAccessor.Commit();
			if (!project.IsDisposed) project.Dispose();
			commitMsg ??= "Auto-commit";
			var projectDir = Path.Combine(baseDir, "webwork", localCode);
			var fwdataPath = Path.Join(projectDir, $"{localCode}.fwdata");
			LfMergeBridge.LfMergeBridge.DisassembleFwdataFile(NullProgress, false, fwdataPath);
			MercurialTestHelper.HgClean(projectDir); // Ensure ConfigurationSettings, etc., don't get committed
			MercurialTestHelper.HgCommit(projectDir, commitMsg);
			MercurialTestHelper.HgPush(projectDir, withAuth.Uri.AbsoluteUri);
		}

		private string TestName
		{
			get
			{
				var testName = TestContext.CurrentContext.Test.Name;
				var firstInvalidChar = testName.IndexOfAny(Path.GetInvalidPathChars());
				if (firstInvalidChar >= 0)
					testName = testName.Substring(0, firstInvalidChar);
				return testName;
			}
		}

	}
}
