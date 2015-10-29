// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using NUnit.Framework;

namespace LfMerge.Tests
{
	[TestFixture]
	public class ProcessingStateTests
	{
		private TestEnvironment _env;

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

		[Test]
		public void Serialization_Roundtrip()
		{
			var expectedState = new ProcessingState("ProjA") {
				SRState = ProcessingState.SendReceiveStates.QUEUED,
				LastStateChangeTicks = DateTime.Now.Ticks,
				PercentComplete = 50,
				ElapsedTimeSeconds = 10,
				TimeRemainingSeconds = 20,
				TotalSteps = 5,
				CurrentStep = 1,
				RetryCounter = 2,
				UncommittedEditCounter = 0
			};
			expectedState.Serialize();
			var state = ProcessingState.Deserialize("ProjA");

			Assert.That(state, Is.EqualTo(expectedState));
		}

		[Test]
		public void Deserialization_NonexistingStateFile_ReturnsDefault()
		{
			var state = ProcessingState.Deserialize("ProjB");
			Assert.That(state.ProjectCode, Is.EqualTo("ProjB"));
			Assert.That(state.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.QUEUED));
		}

		[Test]
		public void Deserialization_FromFile()
		{
			var expectedState = new ProcessingState("ProjC") {
				SRState = ProcessingState.SendReceiveStates.QUEUED,
				LastStateChangeTicks = 635683277459459160,
				PercentComplete = 30,
				ElapsedTimeSeconds = 40,
				TimeRemainingSeconds = 50,
				TotalSteps = 3,
				CurrentStep = 2,
				RetryCounter = 1,
				UncommittedEditCounter = 0
			};
			const string json = "{\"SRState\":0,\"LastStateChangeTicks\":635683277459459160," +
				"\"PercentComplete\":30,\"ElapsedTimeSeconds\":40,\"TimeRemainingSeconds\":50," +
				"\"TotalSteps\":3,\"CurrentStep\":2,\"RetryCounter\":1,\"UncommittedEditCounter\":0," +
				"\"ErrorMessage\":null,\"ErrorCode\":0,\"ProjectCode\":\"ProjC\"}";

			Directory.CreateDirectory(LfMergeSettings.Current.StateDirectory);
			var filename = LfMergeSettings.Current.GetStateFileName("ProjC");
			File.WriteAllText(filename, json);

			var state = ProcessingState.Deserialize("ProjC");
			Assert.That(state, Is.EqualTo(expectedState));
		}

		[Test]
		public void State_SettingPropertySerializesState()
		{
			// Setup
			var ticks = DateTime.Now.Ticks;
			var sut = new ProcessingState("proja") {
				SRState = ProcessingState.SendReceiveStates.QUEUED,
				LastStateChangeTicks = ticks,
				PercentComplete = 50,
				ElapsedTimeSeconds = 10,
				TimeRemainingSeconds = 20,
				TotalSteps = 5,
				CurrentStep = 1,
				RetryCounter = 2,
				UncommittedEditCounter = 0
			};
			var expectedJson = string.Format("{{\"SRState\":{0},\"LastStateChangeTicks\":{1}," +
				"\"PercentComplete\":50,\"ElapsedTimeSeconds\":10,\"TimeRemainingSeconds\":20," +
				"\"TotalSteps\":5,\"CurrentStep\":1,\"RetryCounter\":2,\"UncommittedEditCounter\":0," +
				"\"ErrorMessage\":null,\"ErrorCode\":0,\"ProjectCode\":\"proja\"}}",
				(int)ProcessingState.SendReceiveStates.MERGING, ticks);

			// Exercise
			sut.SRState = ProcessingState.SendReceiveStates.MERGING;

			// Verify
			Directory.CreateDirectory(LfMergeSettings.Current.StateDirectory);
			var filename = LfMergeSettings.Current.GetStateFileName("proja");
			Assert.That(File.ReadAllText(filename), Is.EqualTo(expectedJson));
		}

	}
}

