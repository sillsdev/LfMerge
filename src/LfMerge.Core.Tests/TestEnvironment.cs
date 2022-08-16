// Copyright (c) 2011-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Autofac;
using Chorus.Utilities;
using IniParser.Parser;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.LanguageForge.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Settings;
using NUnit.Framework;
using SIL.IO;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Utils;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests
{
	public class TestEnvironment : IDisposable
	{
		private readonly TemporaryFolder       _languageForgeServerFolder;
		private readonly bool                  _resetLfProjectsDuringCleanup;
		private readonly bool                  _releaseSingletons;
		public           LfMergeSettings       Settings;
		private readonly MongoConnectionDouble _mongoConnection;
		public ILogger Logger => MainClass.Logger;

		static TestEnvironment()
		{
			// Need to call MongoConnectionDouble.Initialize() exactly once, before any tests are run -- so do it here
			MongoConnectionDouble.Initialize();
		}

		public TestEnvironment(bool registerSettingsModelDouble = true,
			bool registerProcessingStateDouble = true,
			bool resetLfProjectsDuringCleanup = true,
			TemporaryFolder languageForgeServerFolder = null)
		{
			_resetLfProjectsDuringCleanup = resetLfProjectsDuringCleanup;
			_languageForgeServerFolder = languageForgeServerFolder ?? new TemporaryFolder(TestName + Path.GetRandomFileName());
			Environment.SetEnvironmentVariable("FW_CommonAppData", _languageForgeServerFolder.Path);
			MainClass.Container = RegisterTypes(registerSettingsModelDouble,
				registerProcessingStateDouble, _languageForgeServerFolder.Path).Build();
			Settings = MainClass.Container.Resolve<LfMergeSettings>();
			MainClass.Logger = MainClass.Container.Resolve<ILogger>();
			Directory.CreateDirectory(Settings.LcmDirectorySettings.ProjectsDirectory);
			Directory.CreateDirectory(Settings.LcmDirectorySettings.TemplateDirectory);
			Directory.CreateDirectory(Settings.StateDirectory);
			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			// only call SingletonsContainer.Release() at the end if we actually create the
			// singleton
			_releaseSingletons = !SingletonsContainer.Contains<CoreGlobalWritingSystemRepository>();
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

		private ContainerBuilder RegisterTypes(bool registerSettingsModel,
			bool registerProcessingStateDouble, string temporaryFolder)
		{
			var containerBuilder = MainClass.RegisterTypes();
			containerBuilder.RegisterType<LfMergeSettingsDouble>()
				.WithParameter(new TypedParameter(typeof(string), temporaryFolder)).SingleInstance()
				.As<LfMergeSettings>();
			containerBuilder.RegisterType<TestLogger>().SingleInstance().As<ILogger>()
				.WithParameter(new TypedParameter(typeof(string), TestName));


			containerBuilder.RegisterType<MongoConnectionDouble>().As<IMongoConnection>().SingleInstance();

			if (registerSettingsModel)
			{
				containerBuilder.RegisterType<ChorusHelperDouble>().As<ChorusHelper>().SingleInstance();
				containerBuilder.RegisterType<MongoProjectRecordFactoryDouble>().As<MongoProjectRecordFactory>();
			}

			var ldProj = new LanguageDepotProjectDouble();
			containerBuilder.RegisterInstance(ldProj)
				.As<ILanguageDepotProject>().AsSelf().SingleInstance();

			if (registerProcessingStateDouble)
			{
				containerBuilder.RegisterType<ProcessingStateFactoryDouble>()
					.As<IProcessingStateDeserialize>().AsSelf().SingleInstance();
			}
			return containerBuilder;
		}

		public void Dispose()
		{
			_mongoConnection?.Reset();

			MainClass.Container?.Dispose();
			MainClass.Container = null;
			if (_resetLfProjectsDuringCleanup)
				LanguageForgeProjectAccessor.Reset();
			_languageForgeServerFolder?.Dispose();
			Settings = null;
			if (_releaseSingletons)
				SingletonsContainer.Release();

			Environment.SetEnvironmentVariable("FW_CommonAppData", null);
		}

		public string LanguageForgeFolder =>
			// get { return Path.Combine(_languageForgeServerFolder.Path, "webwork"); }
			// Should get this from Settings object, but unfortunately we have to resolve this
			// *before* the Settings object is available.
			LangForgeDirFinder.LcmDirectorySettings.ProjectsDirectory;

		public LfMergeSettings LangForgeDirFinder => Settings;

		public string ProjectPath(string projectCode)
		{
			return Path.Combine(LanguageForgeFolder, projectCode);
		}

		public void CreateProjectUpdateFolder(string projectCode)
		{
			Directory.CreateDirectory(ProjectPath(projectCode));
		}

		public static void CopyFwProjectTo(string projectCode, string destDir)
		{
			var dataDir = Path.Combine(FindGitRepoRoot(), "data");
			DirectoryHelper.Copy(Path.Combine(dataDir, projectCode), Path.Combine(destDir, projectCode));

			// Adjust hgrc file
			var hgrc = Path.Combine(destDir, projectCode, ".hg/hgrc");
			if (File.Exists(hgrc))
			{
				var parser = new IniDataParser();
				var iniData = parser.Parse(File.ReadAllText(hgrc));

				iniData["ui"]["username"] = Environment.UserName;

				var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				iniData["merge-tools"]["chorusmerge.executable"] =
					Path.Combine(outputDirectory, "chorusmerge");

				iniData["extensions"]["fixutf8"] = Path.Combine(FindGitRepoRoot(),
					"MercurialExtensions/fixutf8/fixutf8.py");

				var contents = iniData.ToString();
				File.WriteAllText(hgrc, contents);
			}

			Console.WriteLine("Copied {0} to {1}", projectCode, destDir);
		}

		public static string FindGitRepoRoot(string startDir = null)
		{
			if (string.IsNullOrEmpty(startDir))
				startDir = ExecutionEnvironment.DirectoryOfExecutingAssembly;
			while (!Directory.Exists(Path.Combine(startDir, ".git")) && !File.Exists(Path.Combine(startDir, ".git")))
			{
				var di = new DirectoryInfo(startDir);
				if (di.Parent == null) // We've reached the root directory
				{
					// Last-ditch effort: assume we're in output/Debug, even though we never found .git
					return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
				}
				startDir = Path.Combine(startDir, "..");
			}
			return Path.GetFullPath(startDir);
		}

		public static void ChangeFileEncoding(string fileName, Encoding inEncoding, Encoding outEncoding)
		{
			if (!File.Exists(fileName))
				return;

			string fileContent;
			using (var streamReader = new StreamReader(fileName, inEncoding))
			{
				fileContent = streamReader.ReadToEnd();
			}

			using (var streamWriter = new StreamWriter(fileName, false, outEncoding))
			{
				streamWriter.Write(fileContent);
			}
		}

		public static void OverwriteBytesInFile(string fileName, byte[] bytes, int offset)
		{
			if (!File.Exists(fileName))
				return;

			using (var f = new FileStream(fileName, FileMode.OpenOrCreate))
			{
				f.Seek(offset, SeekOrigin.Begin);
				f.Write(bytes, 0, bytes.Length);
			}
		}

		public static void WriteTextFile(string fileName, string content, bool append = false, Encoding encoding = null)
		{
			if (encoding == null)
				encoding = new UTF8Encoding(false);

			using (var writer = new StreamWriter(fileName, append, encoding))
			{
				writer.Write(content);
			}
		}
	}
}

