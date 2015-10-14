// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;

namespace LfMerge.Tests
{
	[TestFixture]
	public class OptionsTests
	{
		[TestCase(new string[0],
			"all", Options.QueueNames.None, null, Options.QueueNames.None, TestName = "No arguments")]
		[TestCase(new[] { "--priority-project", "ProjA" },
			"ProjA", Options.QueueNames.None, null, Options.QueueNames.None, TestName = "Prio project specified")]
		[TestCase(new[] { "-p", "ProjA" },
			"all", Options.QueueNames.None, "ProjA", Options.QueueNames.None, TestName = "Single project specified")]
		[TestCase(new[] { "--priority-queue", "Commit" },
			"all", Options.QueueNames.Commit, null, Options.QueueNames.None, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "Receive" },
			"all", Options.QueueNames.None, null, Options.QueueNames.Receive, TestName = "single queue specified")]
		[TestCase(new[] { "--priority-project", "ProjA", "--priority-queue", "Send" },
			"ProjA", Options.QueueNames.Send, null, Options.QueueNames.None, TestName = "Prio project+queue specified")]
		[TestCase(new[] { "-p", "ProjA", "-q", "Merge" },
			"all", Options.QueueNames.None, "ProjA", Options.QueueNames.Merge, TestName = "Single project+queue specified")]
		[TestCase(new[] { "--priority-project", "ProjA", "-q", "Send" },
			"ProjA", Options.QueueNames.None, null, Options.QueueNames.Send, TestName = "Prio project + single queue specified")]
		[TestCase(new[] { "-p", "ProjA", "--priority-queue", "Merge" },
			"all", Options.QueueNames.Merge, "ProjA", Options.QueueNames.None, TestName = "Single project + prio queue specified")]
		public void ParseArgs(string[] args, string expectedPrioProj,
			Options.QueueNames expectedPrioQueue, string expectedSingleProj,
			Options.QueueNames expectedSingleQueue)
		{
			// SUT
			var options = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(options.PriorityProject, Is.EqualTo(expectedPrioProj));
			Assert.That(options.PriorityQueue, Is.EqualTo(expectedPrioQueue));
			Assert.That(options.SingleProject, Is.EqualTo(expectedSingleProj));
			Assert.That(options.SingleQueue, Is.EqualTo(expectedSingleQueue));
		}

		// The dummy parameter in the method below is necessary to work around a presumable
		// bug in the mono c# compiler. Without it we get a build failure.
		[TestCase(new[] { "--priority-project", "ProjA", "-p", "ProjB" }, "", TestName = "Prio + single project specified")]
		[TestCase(new[] { "--priority-queue", "Commit", "-q", "Merge" }, "", TestName = "Prio + single queue specified")]
		public void ParseArgs_InvalidCombinations(string[] args, string dummy /* see comment above */)
		{
			// SUT
			var options = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(options, Is.Null);
		}

		[TestCase(new string[0],
			"all", false, TestName = "No arguments")]
		[TestCase(new[] { "--priority-project", "ProjA" },
			"ProjA", false, TestName = "Prio project specified")]
		[TestCase(new[] { "-p", "ProjA" },
			"ProjA", true, TestName = "Single project specified")]
		public void FirstProjectAndStopAfterFirstProject(string[] args,
			string expectedFirstProject, bool expectedStop)
		{
			// SUT
			var options = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(options.FirstProject, Is.EqualTo(expectedFirstProject));
			Assert.That(options.StopAfterFirstProject, Is.EqualTo(expectedStop));
		}

		[TestCase(new string[0],
			Options.QueueNames.Merge, false, TestName = "No arguments")]
		[TestCase(new[] { "--priority-queue", "commit" },
			Options.QueueNames.Commit, false, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "receive" },
			Options.QueueNames.Receive, true, TestName = "Single queue specified")]
		public void FirstQueueAndStopAfterFirstQueue(string[] args,
			Options.QueueNames expectedFirstQueue, bool expectedStop)
		{
			// SUT
			var options = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(options.FirstQueue, Is.EqualTo(expectedFirstQueue));
			Assert.That(options.StopAfterFirstQueue, Is.EqualTo(expectedStop));
		}
	}
}

