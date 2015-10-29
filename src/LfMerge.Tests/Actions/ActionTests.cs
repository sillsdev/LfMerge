// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using NUnit.Framework;
using LfMerge;
using LfMerge.Queues;
using LfMerge.Actions;
using LfMerge.FieldWorks;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ActionTests
	{
		class ProcessingStateDouble: ProcessingState
		{
			public List<ProcessingState.SendReceiveStates> SavedStates;

			public ProcessingStateDouble(string projectCode) : base(projectCode)
			{
				SavedStates = new List<ProcessingState.SendReceiveStates>();
			}

			protected override void SetProperty<T>(ref T property, T value)
			{
				property = value;

				if (SavedStates.Count == 0 || SavedStates[SavedStates.Count - 1] != SRState)
					SavedStates.Add(SRState);
			}

			public void ResetSavedStates()
			{
				SavedStates.Clear();
			}
		}

		class LfProjectDouble: ILfProject
		{
			private ProcessingState _state;

			public LfProjectDouble(string projectCode, ProcessingStateDouble state)
			{
				LfProjectCode = projectCode;
				_state = state;
			}

			#region ILfProject implementation

			public string LfProjectCode { get; private set; }

			public FwProject FieldWorksProject
			{
				get
				{
					throw new NotImplementedException();
				}
			}

			public ProcessingState State
			{
				get { return _state; }
			}


			public LanguageDepotProject LanguageDepotProject
			{
				get
				{
					throw new NotImplementedException();
				}
			}
			#endregion
		}

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

		[TestCase(ActionNames.UpdateFdoFromMongoDb, ProcessingState.SendReceiveStates.QUEUED)]
		[TestCase(ActionNames.Commit, ProcessingState.SendReceiveStates.QUEUED)]
		[TestCase(ActionNames.Receive, ProcessingState.SendReceiveStates.RECEIVING)]
		[TestCase(ActionNames.Merge, ProcessingState.SendReceiveStates.MERGING)]
		[TestCase(ActionNames.Send, ProcessingState.SendReceiveStates.SENDING)]
		[TestCase(ActionNames.UpdateMongoDbFromFdo, ProcessingState.SendReceiveStates.UPDATING)]
		public void State(ActionNames actionName, ProcessingState.SendReceiveStates expectedState)
		{
			// Setup
			var state = new ProcessingStateDouble("proja");
			var lfProj = new LfProjectDouble("proja", state);
			var sut = LfMerge.Actions.Action.GetAction(actionName);

			// Exercise
			sut.Run(lfProj);

			// Verify
			Assert.That(state.SavedStates, Is.EqualTo(new[] {
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
			var state = new ProcessingStateDouble("proja");
			var lfProj = new LfProjectDouble("proja", state);
			state.SRState = ProcessingState.SendReceiveStates.HOLD;
			state.ResetSavedStates();
			var sut = LfMerge.Actions.Action.GetAction(actionName);

			// Exercise
			sut.Run(lfProj);

			// Verify
			Assert.That(state.SavedStates, Is.Empty);
		}
	}
}

