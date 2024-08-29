using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Autofac;
using BirdMessenger;
using BirdMessenger.Collections;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using NUnit.Framework;
using SIL.CommandLineProcessing;
using SIL.TestUtilities;

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
		public static Cookie AdminLoginCookie { get; set; }
		public readonly CookieContainer Cookies = new();
		private HttpClientHandler Handler { get; init; }
		private Lazy<HttpClient> LazyHttp { get; init; }
		public HttpClient Http => LazyHttp.Value;
		public readonly SIL.Progress.IProgress NullProgress = new SIL.Progress.NullProgress();
		public readonly ILogger NullLogger = new NullLogger();
		private bool AlreadyLoggedIn = false;
		private TemporaryFolder TempFolder { get; init; }
		private Lazy<GraphQLHttpClient> LazyGqlClient { get; init; }
		private string MongoContainerId { get; set; }
		public GraphQLHttpClient GqlClient => LazyGqlClient.Value;

		public SRTestEnvironment(TemporaryFolder? tempFolder = null)
			: base(true, true, true, tempFolder ?? new TemporaryFolder(TestName + Path.GetRandomFileName()))
		{
			Handler = new() { CookieContainer = Cookies };
			LazyHttp = new(() => new HttpClient(Handler));
			LazyGqlClient = new(() => new GraphQLHttpClient(new Uri(LexboxUrl, "/api/graphql"), new SystemTextJsonSerializer(), Http));
			TempFolder = _languageForgeServerFolder; // Better name for what E2E tests use it for
			Settings.CommitWhenDone = true; // For SR tests specifically, we *do* want changes to .fwdata files to be persisted
		}

		override protected void RegisterMongoConnection(ContainerBuilder builder)
		{
			// E2E tests want a real Mogno connection
			builder.RegisterType<MongoConnection>().As<IMongoConnection>().SingleInstance();
		}

		public void LaunchMongo()
		{
			if (MongoContainerId is null)
			{
				var result = CommandLineRunner.Run("docker", "run -p 27017 -d mongo:6", ".", 30, NullProgress);
				MongoContainerId = result.StandardOutput?.TrimEnd();
				if (string.IsNullOrEmpty(MongoContainerId)) {
					throw new InvalidOperationException("Mongo container failed to start, aborting test");
				}
				result = CommandLineRunner.Run("docker", $"port {MongoContainerId} 27017", ".", 30, NullProgress);
				var hostAndPort = result.StandardOutput?.TrimEnd();
				var parts = hostAndPort.Contains(':') ? hostAndPort.Split(':') : null;
				if (parts is not null && parts.Length == 2) {
					Settings.MongoHostname = parts[0].Replace("0.0.0.0", "localhost");
					Settings.MongoPort = parts[1];
				} else {
					throw new InvalidOperationException($"Mongo container port {hostAndPort} could not be parsed, test will not be able to proceed");
				}
			}
		}

		public void StopMongo()
		{
			if (MongoContainerId is not null)
			{
				CommandLineRunner.Run("docker", $"stop {MongoContainerId}", ".", 30, NullProgress);
				CommandLineRunner.Run("docker", $"rm {MongoContainerId}", ".", 30, NullProgress);
				MongoContainerId = null;
			}
		}

		private bool ShouldStopMongoOnFailure()
		{
			// Mongo container will be torn down on test failure unless LFMERGE_E2E_LEAVE_MONGO_CONTAINER_RUNNING_ON_FAILURE
			// is set to a non-empty value (except "false" or "0", which mean the same as leaving it empty)
			var envVar = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_E2E_LeaveMongoContainerRunningOnFailure)?.Trim();
			return string.IsNullOrEmpty(envVar) || envVar == "false" || envVar == "0";
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing) {
				if (CleanUpTestData || ShouldStopMongoOnFailure()) {
					StopMongo();
				} else {
					Console.WriteLine($"Leaving Mongo container {MongoContainerId} around to examine data on failed test.");
					Console.WriteLine($"It is listening on {Settings.MongoDbHostNameAndPort}");
					Console.WriteLine($"To delete it, run `docker stop {MongoContainerId} ; docker rm {MongoContainerId}`.");
				}
			}
			base.Dispose(disposing);
		}

		public async Task Login()
		{
			if (AlreadyLoggedIn) return;
			if (AdminLoginCookie is null) {
				await LoginAs(LexboxUsername, LexboxPassword);
			} else {
				Cookies.Add(AdminLoginCookie);
				AlreadyLoggedIn = true;
			}
		}

		public async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			if (AlreadyLoggedIn) return;
			var loginResult = await Http.PostAsync(new Uri(LexboxUrl, "api/login"), JsonContent.Create(new { EmailOrUsername=lexboxUsername, Password=lexboxPassword }));
			var cookies = Cookies.GetCookies(LexboxUrl);
			AdminLoginCookie = cookies[".LexBoxAuth"];
			AlreadyLoggedIn = true;
			// Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
			// Bearer auth on LexBox requires logging in to LexBox via their OAuth flow. For now we'll let the cookie container handle it.
		}

		public async Task<bool> IsLexBoxAvailable()
		{
			try {
				var httpResponse = await Http.GetAsync(new Uri(LexboxUrl, "api/healthz"));
				return httpResponse.IsSuccessStatusCode && httpResponse.Headers.TryGetValues("lexbox-version", out var _ignore);
			} catch { return false; }
		}

		public static Uri LexboxUrlForProject(string code) => new Uri(LexboxUrl, $"hg/{code}");
		public static Uri LexboxUrlForProjectWithAuth(string code) => new Uri(LexboxUrlBasicAuth, $"hg/{code}");

		public async Task<LexboxGraphQLTypes.CreateProjectResponse> CreateLexBoxProject(string code, Guid? projId = null, string? name = null, string? description = null, Guid? managerId = null, Guid? orgId = null)
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
			var gqlResponse = await GqlClient.SendMutationAsync<LexboxGraphQLTypes.CreateProjectGqlResponse>(request);
			Assert.That(gqlResponse.Errors, Is.Null.Or.Empty, () => string.Join("\n", gqlResponse.Errors.Select(error => error.Message)));
			var response = gqlResponse.Data.CreateProject.CreateProjectResponse;
			Assert.That(response.Result, Is.EqualTo(LexboxGraphQLTypes.CreateProjectResult.Created));
			Assert.That(response.Id, Is.EqualTo(projId));
			return response;
		}

		public async Task DeleteLexBoxProject(Guid projectId)
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

		public async Task ResetToEmpty(string code)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			var finishResetUrl = new Uri(LexboxUrl, $"api/project/finishResetProject/{code}");
			await Http.PostAsync(finishResetUrl, null);
		}

		public async Task TusUpload(Uri tusEndpoint, string path, string mimeType)
		{
			var file = new FileInfo(path);
			if (!file.Exists) return;
			var metadata = new MetadataCollection { { "filetype", mimeType } };
			var createOpts = new TusCreateRequestOption {
				Endpoint = tusEndpoint,
				UploadLength = file.Length,
				Metadata = metadata,
			};
			var createResponse = await Http.TusCreateAsync(createOpts);

			// That doesn't actually upload the file; TusPatchAsync does the actual upload
			using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
			var patchOpts = new TusPatchRequestOption {
				FileLocation = createResponse.FileLocation,
				Stream = fileStream,
			};
			await Http.TusPatchAsync(patchOpts);
		}

		public Task UploadZip(string code, string zipPath)
		{
			var sourceUrl = new Uri(LexboxUrl, $"/api/project/upload-zip/{code}");
			return TusUpload(sourceUrl, zipPath, "application/zip");
		}

		public async Task DownloadProjectBackup(string code, string destZipPath)
		{
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var filename = result.Content.Headers.ContentDisposition?.FileName;
			using (var outStream = File.Create(destZipPath))
			{
				await result.Content.CopyToAsync(outStream);
			}
		}

		public void CommitAndPush(FwProject project, string code, string baseDir, string? localCode = null, string? commitMsg = null)
		{
			project.Cache.ActionHandlerAccessor.Commit();
			if (!project.IsDisposed) project.Dispose();
			CommitAndPush(code, baseDir, localCode, commitMsg);
		}

		public void CommitAndPush(string code, string baseDir, string? localCode = null, string? commitMsg = null)
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
