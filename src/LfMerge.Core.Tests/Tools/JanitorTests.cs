// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using LfMerge.Core.Logging;
using LfMerge.Core.Queues;
using LfMerge.Core.Tools;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Tools
{
	[TestFixture]
	public class JanitorTests
	{
		private TestEnvironment _env;
		private ExceptionLoggingDouble _exceptionLoggingDouble;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment(registerProcessingStateDouble: false);
			_exceptionLoggingDouble = ExceptionLoggingDouble.Initialize();
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[TestCase(ProcessingState.SendReceiveStates.CLONED)]
		[TestCase(ProcessingState.SendReceiveStates.ERROR)]
		[TestCase(ProcessingState.SendReceiveStates.HOLD)]
		[TestCase(ProcessingState.SendReceiveStates.IDLE)]
		public void CleanupAndRescheduleJobs_NothingToDo(ProcessingState.SendReceiveStates srState)
		{
			// Setup
			var state = new ProcessingState(TestContext.CurrentContext.Test.Name, _env.Settings) {
				SRState = srState
			};
			state.Serialize();

			// Execute
			var janitor = new Janitor(_env.Settings, _env.Logger);
			janitor.CleanupAndRescheduleJobs();

			// Verify
			var queue = Queue.GetQueue(QueueNames.Synchronize);
			Assert.That(queue.IsEmpty, Is.True);

			var newState = ProcessingState.Deserialize(TestContext.CurrentContext.Test.Name);
			Assert.That(newState.SRState, Is.EqualTo(srState));

			Assert.That(_exceptionLoggingDouble.Exceptions.Count, Is.EqualTo(0));
		}

		[TestCase(ProcessingState.SendReceiveStates.CLONING)]
		[TestCase(ProcessingState.SendReceiveStates.SYNCING)]
		public void CleanupAndRescheduleJobs_Reschedule(ProcessingState.SendReceiveStates srState)
		{
			// Setup
			var testName = TestContext.CurrentContext.Test.Name;
			var state = new ProcessingState(testName, _env.Settings) {
				SRState = srState
			};
			state.Serialize();

			// Execute
			var janitor = new Janitor(_env.Settings, _env.Logger);
			janitor.CleanupAndRescheduleJobs();

			// Verify
			var queue = Queue.GetQueue(QueueNames.Synchronize);
			Assert.That(queue.QueuedProjects, Is.EqualTo(new[] { testName }));

			var newState = ProcessingState.Deserialize(testName);
			Assert.That(newState.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.IDLE));

			Assert.That(_exceptionLoggingDouble.Exceptions.Count, Is.EqualTo(1));
			var report = _exceptionLoggingDouble.Exceptions[0];
			Assert.That(report.OriginalException, Is.TypeOf<ProjectInUncleanStateException>());
			Assert.That(report.OriginalException.Message, Is.EqualTo(string.Format(
				"QueueManager detected project 'CleanupAndRescheduleJobs_Reschedule({0})' in unclean state '{0}'; rescheduled",
				srState)));
		}

		[TestCase(42)]
		public void CleanupAndRescheduleJobs_UnknownState(ProcessingState.SendReceiveStates srState)
		{
			// Setup
			var testName = TestContext.CurrentContext.Test.Name;
			var state = new ProcessingState(testName, _env.Settings) {
				SRState = srState
			};
			state.Serialize();

			// Execute
			var janitor = new Janitor(_env.Settings, _env.Logger);
			janitor.CleanupAndRescheduleJobs();

			// Verify
			var queue = Queue.GetQueue(QueueNames.Synchronize);
			Assert.That(queue.QueuedProjects, Is.EqualTo(new[] { testName }));

			var newState = ProcessingState.Deserialize(testName);
			Assert.That(newState.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.IDLE));

			Assert.That(_exceptionLoggingDouble.Exceptions.Count, Is.EqualTo(1));
			var report = _exceptionLoggingDouble.Exceptions[0];
			Assert.That(report.OriginalException, Is.TypeOf<ProjectInUncleanStateException>());
			Assert.That(report.OriginalException.Message, Is.EqualTo("QueueManager detected unknown state '42' for project 'CleanupAndRescheduleJobs_UnknownState(42)'; rescheduled"));
		}
	}
}