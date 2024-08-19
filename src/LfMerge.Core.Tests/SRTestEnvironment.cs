using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using NUnit.Framework;
using SIL.TestUtilities;
using TusDotNetClient;

namespace LfMerge.Core.Tests
{
	/// <summary>
	/// Test environment for end-to-end testing, i.e. Send/Receive with a real LexBox instance
	/// </summary>
	public class SRTestEnvironment : TestEnvironment
	{
		public static readonly string LexboxHostname = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname) ?? "localhost";
		public static readonly string LexboxProtocol = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol) ?? "http";
		public static readonly string LexboxPort = LexboxProtocol == "http" ? "80" : "443";
		public static readonly string LexboxUsername = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername) ?? "admin";
		public static readonly string LexboxPassword = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken) ?? "pass";
		public static readonly Uri LexboxUrl = new($"{LexboxProtocol}://{LexboxHostname}:{LexboxPort}");
		public static readonly Uri LexboxUrlBasicAuth = new($"{LexboxProtocol}://{WebUtility.UrlEncode(LexboxUsername)}:{WebUtility.UrlEncode(LexboxPassword)}@{LexboxHostname}:{LexboxPort}");
		public static readonly CookieContainer Cookies = new();
		private static readonly HttpClientHandler Handler = new() { CookieContainer = Cookies };
		private static readonly Lazy<HttpClient> LazyHttp = new(() => new HttpClient(Handler));
		public static HttpClient Http => LazyHttp.Value;
		public static readonly SIL.Progress.IProgress NullProgress = new SIL.Progress.NullProgress();
		public static readonly ILogger NullLogger = new NullLogger();
		private static bool AlreadyLoggedIn = false;
		private TemporaryFolder TempFolder { get; init; }
		private static readonly Lazy<GraphQLHttpClient> LazyGqlClient = new(() => new GraphQLHttpClient(new Uri(LexboxUrl, "/api/graphql"), new SystemTextJsonSerializer(), Http));
		public static GraphQLHttpClient GqlClient => LazyGqlClient.Value;

		public SRTestEnvironment(TemporaryFolder? tempFolder = null)
			: base(true, true, true, tempFolder ?? new TemporaryFolder(TestName + Path.GetRandomFileName()))
		{
			TempFolder = _languageForgeServerFolder; // Better name for what E2E tests use it for
			Settings.CommitWhenDone = true; // For SR tests specifically, we *do* want changes to .fwdata files to be persisted
		}

		public static Task Login()
		{
			return LoginAs(LexboxUsername, LexboxPassword);
		}

		public static async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			if (AlreadyLoggedIn) return;
			var loginResult = await Http.PostAsync(new Uri(LexboxUrl, "api/login"), JsonContent.Create(new { EmailOrUsername=lexboxUsername, Password=lexboxPassword }));
			var cookies = Cookies.GetCookies(LexboxUrl);
			AlreadyLoggedIn = true;
			// Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
			// Bearer auth on LexBox requires logging in to LexBox via their OAuth flow. For now we'll let the cookie container handle it.
		}

		public static Uri LexboxUrlForProject(string code) => new Uri(LexboxUrl, $"hg/{code}");
		public static Uri LexboxUrlForProjectWithAuth(string code) => new Uri(LexboxUrlBasicAuth, $"hg/{code}");

		public static async Task<LexboxGraphQLTypes.CreateProjectResponse> CreateLexBoxProject(string code, Guid? projId = null, string? name = null, string? description = null, Guid? managerId = null, Guid? orgId = null)
		{
			projId ??= Guid.NewGuid();
			name ??= code;
			description ??= $"Auto-created project for test {TestName}";
			var mutation = """
			mutation createProject($input: CreateProjectInput!) {
				createProject(input: $input) {
					createProjectResponse {
						id
						result
					}
					errors {
						... on DbError {
							code
						}
					}
				}
			}
			""";
			var input = new LexboxGraphQLTypes.CreateProjectInput(projId, name, description, code, LexboxGraphQLTypes.ProjectType.FLEx, LexboxGraphQLTypes.RetentionPolicy.Dev, false, managerId, orgId);
			var request = new GraphQLRequest {
				Query = mutation,
				Variables = new { input },
			};
			var response = await GqlClient.SendMutationAsync<LexboxGraphQLTypes.CreateProjectGqlResponse>(request);
			Assert.That(response.Errors, Is.Null.Or.Empty, () => string.Join("\n", response.Errors.Select(error => error.Message)));
			return response.Data.CreateProject.CreateProjectResponse;
		}

		public static async Task DeleteLexBoxProject(Guid projectId)
		{
			var mutation = """
			mutation SoftDeleteProject($input: SoftDeleteProjectInput!) {
				softDeleteProject(input: $input) {
					project {
						id
					}
					errors {
						... on Error {
							message
						}
					}
				}
			}
			""";
			var input = new { projectId };
			var request = new GraphQLRequest {
				Query = mutation,
				Variables = new { input },
			};
			var response = await GqlClient.SendMutationAsync<object>(request);
			Assert.That(response.Errors, Is.Null.Or.Empty, () => string.Join("\n", response.Errors.Select(error => error.Message)));
		}

		public static void InitRepo(string code, string dest)
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

		public static async Task ResetToEmpty(string code)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			var finishResetUrl = new Uri(LexboxUrl, $"api/project/finishResetProject/{code}");
			await Http.PostAsync(finishResetUrl, null);
		}

		public static async Task UploadZip(string code, string zipPath)
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

		public static async Task DownloadProjectBackup(string code, string destZipPath)
		{
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var filename = result.Content.Headers.ContentDisposition?.FileName;
			using (var outStream = File.Create(destZipPath))
			{
				await result.Content.CopyToAsync(outStream);
			}
		}

		public static void CommitAndPush(FwProject project, string code, string baseDir, string? localCode = null, string? commitMsg = null)
		{
			project.Cache.ActionHandlerAccessor.Commit();
			if (!project.IsDisposed) project.Dispose();
			CommitAndPush(code, baseDir, localCode, commitMsg);
		}

		public static void CommitAndPush(string code, string baseDir, string? localCode = null, string? commitMsg = null)
		{
			localCode ??= code;
			var projUrl = new Uri(LexboxUrl, $"/hg/{code}");
			var withAuth = new UriBuilder(projUrl) { UserName = "admin", Password = "pass" };
			commitMsg ??= "Auto-commit";
			var projectDir = Path.Combine(baseDir, "webwork", localCode);
			var fwdataPath = Path.Join(projectDir, $"{localCode}.fwdata");
			LfMergeBridge.LfMergeBridge.DisassembleFwdataFile(NullProgress, false, fwdataPath);
			MercurialTestHelper.HgClean(projectDir); // Ensure ConfigurationSettings, etc., don't get committed
			MercurialTestHelper.HgCommit(projectDir, commitMsg);
			MercurialTestHelper.HgPush(projectDir, withAuth.Uri.AbsoluteUri);
		}

		private static string TestName
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
