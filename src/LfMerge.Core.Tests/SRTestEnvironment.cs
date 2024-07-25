using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Bugsnag.Payload;
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
		private TemporaryFolder TempFolder { get; init; }
		private HttpClient Http { get; init; } = new HttpClient();
		private string Jwt { get; set; }

		public SRTestEnvironment(string lexboxHostname = "localhost", string lexboxProtocol = "http", int lexboxPort = 80, string lexboxUsername = "admin", string lexboxPassword = "pass")
		{
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPrivateHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol, lexboxProtocol);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_HgUsername, lexboxUsername);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_TrustToken, lexboxPassword);
			LexboxUrl = new Uri($"{lexboxProtocol}://{lexboxHostname}:{lexboxPort}");
			LexboxUrlBasicAuth = new Uri($"{lexboxProtocol}://{WebUtility.UrlEncode(lexboxUsername)}:{WebUtility.UrlEncode(lexboxPassword)}@{lexboxHostname}:{lexboxPort}");
			TempFolder = new TemporaryFolder(TestName + Path.GetRandomFileName());
		}

		public Task Login()
		{
			var lexboxUsername = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername);
			var lexboxPassword = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken);
			return LoginAs(lexboxUsername, lexboxPassword);
		}

		public async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			var loginResult = await Http.PostAsJsonAsync(new Uri(LexboxUrl, "api/login"), new { EmailOrUsername=lexboxUsername, Password=lexboxPassword });
			Jwt = await loginResult.Content.ReadAsStringAsync();
			Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
		}

		public void InitRepo(string code, string dest)
		{
			var sourceUrl = new Uri(LexboxUrlBasicAuth, $"hg/{code}");
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
			var sourceUrl = new Uri(LexboxUrl, $"api/project/upload-zip/{code}");
			var file = new FileInfo(zipPath);
			var client = new TusClient();
			client.AdditionalHeaders["Authorization"] = $"Bearer {Jwt}";
			var fileUrl = await client.CreateAsync(sourceUrl.AbsolutePath, file.Length, []);
			await client.UploadAsync(fileUrl, file);
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
