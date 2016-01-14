// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using Autofac;
using NUnit.Framework;
using LfMerge.Actions;
using LfMerge.Queues;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ActionTests
	{
		private TestEnvironment _env;

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			// Force setting of Options.Current
			new Options();
		}

		private ProcessingStateDouble ProcessState
		{
			get
			{
				var factory = MainClass.Container.Resolve<ProcessingStateFactoryDouble>();
				return factory.State;
			}
		}

		private ProcessingStateFactoryDouble Factory
		{
			get
			{
				return MainClass.Container.Resolve<ProcessingStateFactoryDouble>();
			}
		}

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
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

		[TestCase(ActionNames.UpdateFdoFromMongoDb, ProcessingState.SendReceiveStates.QUEUED)]
		[TestCase(ActionNames.Commit, ProcessingState.SendReceiveStates.QUEUED)]
		[TestCase(ActionNames.Receive, ProcessingState.SendReceiveStates.RECEIVING)]
		[TestCase(ActionNames.Merge, ProcessingState.SendReceiveStates.MERGING)]
		[TestCase(ActionNames.Send, ProcessingState.SendReceiveStates.SENDING)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo, ProcessingState.SendReceiveStates.UPDATING)]
		public void State(ActionNames actionName, ProcessingState.SendReceiveStates expectedState)
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, "proja");
			var sut = Action.GetAction(actionName);

			// Exercise
			sut.Run(lfProj);

			// Verify
			Assert.That(ProcessState.SavedStates, Is.EqualTo(new[] {
				expectedState, ProcessingState.SendReceiveStates.IDLE }));
			Assert.That(lfProj.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.IDLE));
		}

		[TestCase(ActionNames.UpdateFdoFromMongoDb)]
		[TestCase(ActionNames.Commit)]
		[TestCase(ActionNames.Receive)]
		[TestCase(ActionNames.Merge)]
		[TestCase(ActionNames.Send)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo)]
		public void State_SkipsHoldState(ActionNames actionName)
		{
			// Setup
			var lfProj = LanguageForgeProject.Create(_env.Settings, "proja");
			var state = Factory.Deserialize("proja") as ProcessingStateDouble;
			state.SRState = ProcessingState.SendReceiveStates.HOLD;
			state.ResetSavedStates();
			Factory.State = state;
			var sut = Action.GetAction(actionName);

			// Exercise
			sut.Run(lfProj);

			// Verify
			Assert.That(ProcessState.SavedStates, Is.Empty);
		}
	}
}

