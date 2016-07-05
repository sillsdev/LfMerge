// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Palaso.CommandLineProcessing;
using Palaso.Progress;

namespace LfMerge.Tests
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
			Assert.That(regex.IsMatch(result.StandardOutput), Is.True);
			var match = regex.Match(result.StandardOutput);
			Url = match.Groups[1].Captures[0].Value;
		}

		public void Stop()
		{
			if (string.IsNullOrEmpty(_pidFile) || !File.Exists(_pidFile))
				return;

			var pid = File.ReadAllText(_pidFile);
			CommandLineRunner.Run("kill", "-9 " + pid, Directory.GetCurrentDirectory(),
				120, new NullProgress());
			File.Delete(_pidFile);
		}

		public string Url { get; private set; }

		public string RepoPath { get; private set; }
	}
}
