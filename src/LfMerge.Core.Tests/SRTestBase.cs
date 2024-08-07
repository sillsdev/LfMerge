using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using SIL.LCModel;
using SIL.TestUtilities;
using TusDotNetClient;

namespace LfMerge.Core.Tests
{
	/// <summary>
	/// Test base class for end-to-end testing, i.e. Send/Receive with a real LexBox instance
	/// </summary>
	public class SRTestBase
	{
		public ILogger Logger => MainClass.Logger;
		public Uri LexboxUrl { get; init; }
		public Uri LexboxUrlBasicAuth { get; init; }
		private TemporaryFolder TempFolder { get; init; }
		private HttpClient Http { get; init; }
		private HttpClientHandler Handler { get; init; } = new HttpClientHandler();
		private CookieContainer Cookies { get; init; } = new CookieContainer();
		private string Jwt { get; set; }
		private string TipRevToRestore { get; set; } = "";

		public SRTestBase(string lexboxHostname = "localhost", string lexboxProtocol = "http", int lexboxPort = 80, string lexboxUsername = "admin", string lexboxPassword = "pass")
		{
			// TODO: Just get an SRTestEnvironment instead of all this
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPrivateHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol, lexboxProtocol);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_HgUsername, lexboxUsername);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_TrustToken, lexboxPassword);
			LexboxUrl = new Uri($"{lexboxProtocol}://{lexboxHostname}:{lexboxPort}");
			LexboxUrlBasicAuth = new Uri($"{lexboxProtocol}://{WebUtility.UrlEncode(lexboxUsername)}:{WebUtility.UrlEncode(lexboxPassword)}@{lexboxHostname}:{lexboxPort}");
			TempFolder = new TemporaryFolder(TestName + Path.GetRandomFileName());
			Handler.CookieContainer = Cookies;
			Http = new HttpClient(Handler);
		}

		public Task Login()
		{
			var lexboxUsername = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername);
			var lexboxPassword = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken);
			return LoginAs(lexboxUsername, lexboxPassword);
		}

		public async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			var loginResult = await Http.PostAsync(new Uri(LexboxUrl, "api/login"), JsonContent.Create(new { EmailOrUsername=lexboxUsername, Password=lexboxPassword }));
			var cookies = Cookies.GetCookies(LexboxUrl);
			Jwt = cookies[".LexBoxAuth"].Value;
			// Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
			// Bearer auth on LexBox requires logging in to LexBox via their OAuth flow. For now we'll let the cookie container handle it.
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

		[SetUp]
		public async Task BackupRemoteProject()
		{
			var env = new SRTestEnvironment(); // TODO: Instance property
			var test = TestContext.CurrentContext.Test;
			if (test.Properties.ContainsKey("projectCode")) {
				var code = test.Properties.Get("projectCode") as string;
				TipRevToRestore = await env.GetTipRev(code);
			} else {
				TipRevToRestore = "";
			}
		}

		[TearDown]
		public async Task RestoreRemoteProject()
		{
			var env = new SRTestEnvironment(); // TODO: Instance property
			var test = TestContext.CurrentContext.Test;
			if (!string.IsNullOrEmpty(TipRevToRestore) && test.Properties.ContainsKey("projectCode")) {
				var code = test.Properties.Get("projectCode") as string;
				await env.RollbackProjectToRev(code, TipRevToRestore);
			}
		}
	}
}
