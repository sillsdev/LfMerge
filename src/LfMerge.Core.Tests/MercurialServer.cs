// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SIL.CommandLineProcessing;
using SIL.PlatformUtilities;
using SIL.Progress;

namespace LfMerge.Core.Tests
{
	public class MercurialServer
	{
		private string _pidFile;

		public MercurialServer(string repoPath)
		{
			RepoPath = repoPath;
		}

		public void Start()
		{
			_pidFile = Path.GetTempFileName();
			var args = "serv --port 0 --daemon --config web.push_ssl=No --config \"web.allow_push=*\" --pid-file "
				+ _pidFile;
			var result = CommandLineRunner.Run(MercurialTestHelper.HgCommand,
				args, RepoPath, 120, new NullProgress());
			Assert.That(result.ExitCode, Is.EqualTo(0),
				string.Format("hg {0}\nStdOut: {1}\nStdErr: {2}", args,
					result.StandardOutput, result.StandardError));
			var regex = new Regex("^listening at ([^ ]+)");
			if (Platform.IsLinux)
			{
				Assert.That(regex.IsMatch(result.StandardOutput), Is.True);
				var match = regex.Match(result.StandardOutput);
				Url = match.Groups[1].Captures[0].Value;
			}

			IsStarted = true;
		}

		public void Stop()
		{
			if (string.IsNullOrEmpty(_pidFile) || !File.Exists(_pidFile))
				return;

			var pid = File.ReadAllText(_pidFile);
			if (Platform.IsLinux)
			{
				CommandLineRunner.Run("kill", "-9 " + pid, Directory.GetCurrentDirectory(),
					120, new NullProgress());
			}
			else
			{
				CommandLineRunner.Run("taskkill", "/F /PID" + pid, Directory.GetCurrentDirectory(),
					120, new NullProgress());
			}

			File.Delete(_pidFile);
			IsStarted = false;
		}

		public string Url { get; private set; }

		public string RepoPath { get; private set; }

		public bool IsStarted { get; private set; }
	}
}
