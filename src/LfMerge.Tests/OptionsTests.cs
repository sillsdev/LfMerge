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
		[TestCase(new[] { "-p", "ProjA" },
			"ProjA", QueueNames.None, null, QueueNames.None, TestName = "Prio project specified")]
		public void ParseArgs(string[] args, string expectedPrioProj,
			QueueNames expectedPrioQueue, string expectedSingleProj,
			QueueNames expectedSingleQueue)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.PriorityProject, Is.EqualTo(expectedPrioProj));
			Assert.That(sut.PriorityProject, Is.EqualTo(expectedSingleProj));
		}

		[TestCase(new string[0],
			"all", false, TestName = "No arguments")]
		[TestCase(new[] { "-p", "ProjA" },
			"ProjA", false, TestName = "Prio project specified")]
		public void FirstProjectAndStopAfterFirstProject(string[] args,
			string expectedFirstProject, bool expectedStop)
		{
			var sut = Options.ParseCommandLineArgs(args);

			// Verify
			Assert.That(sut.FirstProject, Is.EqualTo(expectedFirstProject));
			Assert.That(sut.StopAfterFirstProject, Is.EqualTo(expectedStop));
		}

		[TestCase(QueueNames.None, ActionNames.None)]
		[TestCase(QueueNames.Edit, ActionNames.UpdateFdoFromMongoDb)]
		[TestCase(QueueNames.Synchronize, ActionNames.Synchronize)]
		public void GetActionFromQueue(QueueNames queue, ActionNames expectedAction)
		{
			Assert.That(Options.GetActionForQueue(queue), Is.EqualTo(expectedAction));
		}

		[TestCase(ActionNames.None, QueueNames.None)]
		[TestCase(ActionNames.UpdateFdoFromMongoDb, QueueNames.Edit)]
		[TestCase(ActionNames.Commit, QueueNames.None)]
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

	}
}

