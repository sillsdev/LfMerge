// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using LfMerge.Queues;

namespace LfMerge.Tests
{
	[TestFixture]
	public class OptionsTests
	{
		[TestCase(new string[0],
			"all", QueueNames.None, null, QueueNames.None, TestName = "No arguments")]
		[TestCase(new[] { "--priority-project", "ProjA" },
			"ProjA", QueueNames.None, null, QueueNames.None, TestName = "Prio project specified")]
		[TestCase(new[] { "-p", "ProjA" },
			"all", QueueNames.None, "ProjA", QueueNames.None, TestName = "Single project specified")]
		[TestCase(new[] { "--priority-queue", "Commit" },
			"all", QueueNames.Commit, null, QueueNames.None, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "Receive" },
			"all", QueueNames.None, null, QueueNames.Receive, TestName = "single queue specified")]
		[TestCase(new[] { "--priority-project", "ProjA", "--priority-queue", "Send" },
			"ProjA", QueueNames.Send, null, QueueNames.None, TestName = "Prio project+queue specified")]
		[TestCase(new[] { "-p", "ProjA", "-q", "Merge" },
			"all", QueueNames.None, "ProjA", QueueNames.Merge, TestName = "Single project+queue specified")]
		[TestCase(new[] { "--priority-project", "ProjA", "-q", "Send" },
			"ProjA", QueueNames.None, null, QueueNames.Send, TestName = "Prio project + single queue specified")]
		[TestCase(new[] { "-p", "ProjA", "--priority-queue", "Merge" },
			"all", QueueNames.Merge, "ProjA", QueueNames.None, TestName = "Single project + prio queue specified")]
		public void ParseArgs(string[] args, string expectedPrioProj,
			QueueNames expectedPrioQueue, string expectedSingleProj,
			QueueNames expectedSingleQueue)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.PriorityProject, Is.EqualTo(expectedPrioProj));
			Assert.That(sut.PriorityQueue, Is.EqualTo(expectedPrioQueue));
			Assert.That(sut.SingleProject, Is.EqualTo(expectedSingleProj));
			Assert.That(sut.SingleQueue, Is.EqualTo(expectedSingleQueue));
		}

		// The dummy parameter in the method below is necessary to work around a presumable
		// bug in the mono c# compiler. Without it we get a build failure.
		[TestCase(new[] { "--priority-project", "ProjA", "-p", "ProjB" }, "", TestName = "Prio + single project specified")]
		[TestCase(new[] { "--priority-queue", "Commit", "-q", "Merge" }, "", TestName = "Prio + single queue specified")]
		public void ParseArgs_InvalidCombinations(string[] args, string dummy /* see comment above */)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut, Is.Null);
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
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.FirstProject, Is.EqualTo(expectedFirstProject));
			Assert.That(sut.StopAfterFirstProject, Is.EqualTo(expectedStop));
		}

		[TestCase(new string[0],
			Actions.UpdateFdoFromMongoDb, false, TestName = "No arguments")]
		[TestCase(new[] { "--priority-queue", "commit" },
			Actions.Commit, false, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "receive" },
			Actions.Receive, true, TestName = "Single queue specified")]
		public void FirstActionAndStopAfterFirstAction(string[] args,
			Actions expectedFirstAction, bool expectedStop)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.FirstAction, Is.EqualTo(expectedFirstAction));
			Assert.That(sut.StopAfterFirstAction, Is.EqualTo(expectedStop));
		}

		[TestCase(QueueNames.None, Actions.None)]
		[TestCase(QueueNames.Commit, Actions.Commit)]
		[TestCase(QueueNames.Merge, Actions.UpdateFdoFromMongoDb)]
		[TestCase(QueueNames.Receive, Actions.Receive)]
		[TestCase(QueueNames.Send, Actions.Send)]
		public void GetActionFromQueue(QueueNames queue, Actions expectedAction)
		{
			Assert.That(Options.GetActionForQueue(queue), Is.EqualTo(expectedAction));
		}

		[TestCase(Actions.None, QueueNames.None)]
		[TestCase(Actions.UpdateFdoFromMongoDb, QueueNames.Merge)]
		[TestCase(Actions.Commit, QueueNames.Commit)]
		[TestCase(Actions.Receive, QueueNames.Receive)]
		[TestCase(Actions.Merge, QueueNames.None)]
		[TestCase(Actions.Send, QueueNames.Send)]
		[TestCase(Actions.UpdateMongoDbFromFdo, QueueNames.None)]
		public void GetQueueFromAction(Actions action, QueueNames expectedQueue)
		{
			Assert.That(Options.GetQueueForAction(action), Is.EqualTo(expectedQueue));
		}

		[TestCase(Actions.None, Actions.UpdateFdoFromMongoDb)]
		[TestCase(Actions.UpdateFdoFromMongoDb, Actions.Commit)]
		[TestCase(Actions.Commit, Actions.Receive)]
		[TestCase(Actions.Receive, Actions.Merge)]
		[TestCase(Actions.Merge, Actions.Send)]
		[TestCase(Actions.Send, Actions.UpdateMongoDbFromFdo)]
		[TestCase(Actions.UpdateMongoDbFromFdo, Actions.None)]
		public void GetNextAction(Actions currentAction, Actions expectedAction)
		{
			var option = new Options();
			Assert.That(option.GetNextAction(currentAction), Is.EqualTo(expectedAction));
		}

		[TestCase(Actions.None)]
		[TestCase(Actions.UpdateFdoFromMongoDb)]
		[TestCase(Actions.Commit)]
		[TestCase(Actions.Receive)]
		[TestCase(Actions.Merge)]
		[TestCase(Actions.Send)]
		[TestCase(Actions.UpdateMongoDbFromFdo)]
		public void GetNextActionIfStopAfterFirstAction_ReturnsNone(Actions currentAction)
		{
			var sut = Options.ParseCommandLineArgs(new[] { "-q", "merge" });
			Assert.That(sut.GetNextAction(currentAction), Is.EqualTo(Actions.None));
		}

	}
}

