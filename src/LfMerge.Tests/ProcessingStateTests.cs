// Copyright (c) 2011-2016 SIL International
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
			_env = new TestEnvironment(registerProcessingStateDouble: false);
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void Serialization_Roundtrip()
		{
			var expectedState = new ProcessingState("ProjA", _env.Settings) {
				SRState = ProcessingState.SendReceiveStates.SYNCING,
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
		public void Deserialize_NonexistingStateFile_ReturnsCloning()
		{
			var state = ProcessingState.Deserialize("ProjB");
			Assert.That(state.ProjectCode, Is.EqualTo("ProjB"));
			Assert.That(state.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
		}

		[Test]
		public void Deserialize_InvalidStateFile_DoesntThrow()
		{
			const string json = "badJson";

			Directory.CreateDirectory(_env.Settings.StateDirectory);
			var filename = _env.Settings.GetStateFileName("ProjC");
			File.WriteAllText(filename, json);
			Assert.DoesNotThrow(() =>
			{
				var state = ProcessingState.Deserialize("ProjC");
				Assert.That(state.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			});
		}

		[Test]
		public void Deserialize_EmptyStateFile_DoesntThrow()
		{
			const string json = "";

			Directory.CreateDirectory(_env.Settings.StateDirectory);
			var filename = _env.Settings.GetStateFileName("ProjC");
			File.WriteAllText(filename, json);
			Assert.DoesNotThrow(() =>
			{
				var state = ProcessingState.Deserialize("ProjC");
				Assert.That(state.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			});
		}

		[Test]
		public void Deserialize_ValidStateFile_MatchesState()
		{
			var expectedState = new ProcessingState("ProjC", _env.Settings) {
				SRState = ProcessingState.SendReceiveStates.SYNCING,
				PercentComplete = 30,
				ElapsedTimeSeconds = 40,
				TimeRemainingSeconds = 50,
				TotalSteps = 3,
				CurrentStep = 2,
				RetryCounter = 1,
				UncommittedEditCounter = 0
			};
			expectedState.LastStateChangeTicks = 635683277459459160; // Make sure this gets set last so the value isn't UtcNow.Ticks
			const string json = "{\"SRState\":\"SYNCING\",\"LastStateChangeTicks\":635683277459459160," +
				"\"PercentComplete\":30,\"ElapsedTimeSeconds\":40,\"TimeRemainingSeconds\":50," +
				"\"TotalSteps\":3,\"CurrentStep\":2,\"RetryCounter\":1,\"UncommittedEditCounter\":0," +
				"\"ErrorMessage\":null,\"ErrorCode\":0,\"ProjectCode\":\"ProjC\"}";

			Directory.CreateDirectory(_env.Settings.StateDirectory);
			var filename = _env.Settings.GetStateFileName("ProjC");
			File.WriteAllText(filename, json);

			var state = ProcessingState.Deserialize("ProjC");
			Assert.That(state, Is.EqualTo(expectedState));
		}

		[Test]
		public void State_SettingProperty_SerializesState()
		{
			// Setup
			var ticks = DateTime.Now.Ticks;
			var sut = new ProcessingState("proja", _env.Settings) {
				SRState = ProcessingState.SendReceiveStates.SYNCING,
				LastStateChangeTicks = ticks,
				PercentComplete = 50,
				ElapsedTimeSeconds = 10,
				TimeRemainingSeconds = 20,
				TotalSteps = 5,
				CurrentStep = 1,
				RetryCounter = 2,
				UncommittedEditCounter = 0
			};
			var expectedJson = string.Format(@"{{
  ""SRState"": ""{0}"",
  ""LastStateChangeTicks"": {1},
  ""PercentComplete"": 50,
  ""ElapsedTimeSeconds"": 10,
  ""TimeRemainingSeconds"": 20,
  ""TotalSteps"": 5,
  ""CurrentStep"": 1,
  ""RetryCounter"": 2,
  ""UncommittedEditCounter"": 0,
  ""ErrorMessage"": null,
  ""ErrorCode"": 0,
  ""ProjectCode"": ""proja""
}}", ProcessingState.SendReceiveStates.SYNCING, ticks);

			// Exercise
			sut.SRState = ProcessingState.SendReceiveStates.SYNCING;

			// Verify
			Directory.CreateDirectory(_env.Settings.StateDirectory);
			var filename = _env.Settings.GetStateFileName("proja");
			Assert.That(File.ReadAllText(filename), Is.EqualTo(expectedJson));
		}

	}
}

