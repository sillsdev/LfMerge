// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using NUnit.Framework;
using LfMerge.Queues;
using LfMerge.Actions;

namespace LfMerge.Tests
{
	[TestFixture]
	public class OptionsTests
	{
		[TestCase(new string[0],
			"all", QueueNames.None, null, QueueNames.None, TestName = "No arguments")]
		[TestCase(new[] { "-priority-project", "ProjA" },
			"ProjA", QueueNames.None, null, QueueNames.None, TestName = "Prio project specified")]
		[TestCase(new[] { "-p", "ProjA" },
			"all", QueueNames.None, "ProjA", QueueNames.None, TestName = "Single project specified")]
		[TestCase(new[] { "--priority-queue", "Commit" },
			"all", QueueNames.Commit, null, QueueNames.None, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "Receive" },
			"all", QueueNames.None, null, QueueNames.Synchronize, TestName = "single queue specified")]
		[TestCase(new[] { "-p", "ProjA", "-q", "Merge" },
			"all", QueueNames.None, "ProjA", QueueNames.Edit, TestName = "Single project+queue specified")]
		[TestCase(new[] { "-p", "ProjA", "--priority-queue", "Merge" },
			"all", QueueNames.Edit, "ProjA", QueueNames.None, TestName = "Single project + prio queue specified")]
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
			ActionNames.UpdateFdoFromMongoDb, false, TestName = "No arguments")]
		[TestCase(new[] { "--priority-queue", "commit" },
			ActionNames.Commit, false, TestName = "Prio queue specified")]
		[TestCase(new[] { "-q", "receive" },
			ActionNames.Synchronize, true, TestName = "Single queue specified")]
		public void FirstActionAndStopAfterFirstAction(string[] args,
			ActionNames expectedFirstAction, bool expectedStop)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.FirstAction, Is.EqualTo(expectedFirstAction));
			Assert.That(sut.StopAfterFirstAction, Is.EqualTo(expectedStop));
		}

		[TestCase(QueueNames.None, ActionNames.None)]
		[TestCase(QueueNames.Commit, ActionNames.Commit)]
		[TestCase(QueueNames.Edit, ActionNames.UpdateFdoFromMongoDb)]
		[TestCase(QueueNames.Synchronize, ActionNames.Synchronize)]
		public void GetActionFromQueue(QueueNames queue, ActionNames expectedAction)
		{
			Assert.That(Options.GetActionForQueue(queue), Is.EqualTo(expectedAction));
		}

		[TestCase(ActionNames.None, QueueNames.None)]
		[TestCase(ActionNames.UpdateFdoFromMongoDb, QueueNames.Edit)]
		[TestCase(ActionNames.Commit, QueueNames.Commit)]
		[TestCase(ActionNames.Synchronize, QueueNames.Synchronize)]
		[TestCase(ActionNames.Edit, QueueNames.None)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo, QueueNames.None)]
		public void GetQueueFromAction(ActionNames action, QueueNames expectedQueue)
		{
			Assert.That(Options.GetQueueForAction(action), Is.EqualTo(expectedQueue));
		}

		[TestCase(ActionNames.None, ActionNames.UpdateFdoFromMongoDb)]
		[TestCase(ActionNames.UpdateFdoFromMongoDb, ActionNames.Commit)]
		[TestCase(ActionNames.Commit, ActionNames.Synchronize)]
		[TestCase(ActionNames.Synchronize, ActionNames.Edit)]
		[TestCase(ActionNames.Edit, ActionNames.UpdateMongoDbFromFdo)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo, ActionNames.None)]
		public void GetNextAction(ActionNames currentAction, ActionNames expectedAction)
		{
			var option = new Options();
			Assert.That(option.GetNextAction(currentAction), Is.EqualTo(expectedAction));
		}

		[TestCase(ActionNames.None)]
		[TestCase(ActionNames.UpdateFdoFromMongoDb)]
		[TestCase(ActionNames.Commit)]
		[TestCase(ActionNames.Synchronize)]
		[TestCase(ActionNames.Edit)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo)]
		public void GetNextActionIfStopAfterFirstAction_ReturnsNone(ActionNames currentAction)
		{
			var sut = Options.ParseCommandLineArgs(new[] { "-q", "merge" });
			Assert.That(sut.GetNextAction(currentAction), Is.EqualTo(ActionNames.None));
		}

	}
}

