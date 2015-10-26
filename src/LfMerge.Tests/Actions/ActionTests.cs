// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using NUnit.Framework;
using LfMerge.Queues;
using LfMerge.Actions;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ActionTests
	{
		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			// Force setting of Options.Current
			new Options();
		}

		[TestCase(QueueNames.Merge, new[] { ActionNames.UpdateFdoFromMongoDb })]
		[TestCase(QueueNames.Commit, new[] { ActionNames.Commit })]
		[TestCase(QueueNames.Receive, new[] { ActionNames.Receive, ActionNames.Merge })]
		[TestCase(QueueNames.Send, new[] { ActionNames.Send, ActionNames.UpdateMongoDbFromFdo })]
		public void NextAction(QueueNames queueName, ActionNames[] expectedActionNames)
		{
			var actions = new List<ActionNames>();
			for (var sut = Queue.GetQueue(queueName).CurrentAction;
				sut != null;
				sut = sut.NextAction)
			{
				actions.Add(sut.Name);
			}

			Assert.That(actions, Is.EquivalentTo(expectedActionNames));
		}
	}
}

