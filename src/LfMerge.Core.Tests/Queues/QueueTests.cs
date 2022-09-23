// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.IO;
using System.Threading;
using LfMerge.Core.Actions;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Queues
{
	[TestFixture]
	public class QueueTests
	{
		/// <summary>
		/// Test double for Queue class. This test double allows to set the list of projects.
		/// This simulates getting the list of files sorted by time and saves the time we'd have
		/// to wait between creating the different files.
		/// </summary>
		private class QueueDouble: Queue
		{
			public QueueDouble(LfMergeSettings settings): base(settings)
			{
			}

			protected override string[] RawQueuedProjects => ProjectsForTesting;

			public string[] ProjectsForTesting { get; set; }
		}

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
		public void IsEmpty_NoFiles_ReturnsEmpty()
		{
			var queueDir = _env.Settings.GetQueueDirectory();
			Directory.CreateDirectory(queueDir);
			var sut = new Queue(_env.Settings);

			// Exercise
			var isEmpty = sut.IsEmpty;

			// Verify
			Assert.That(isEmpty, Is.True, "Queue doesn't report it is empty");
		}

		[Test]
		public void IsEmpty_ExistingFiles_ReturnsNotEmpty()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory();
			Directory.CreateDirectory(editQueueDir);
			File.WriteAllText(Path.Combine(editQueueDir, "proja"), string.Empty);
			var sut = new Queue(_env.Settings);

			// Exercise
			var isEmpty = sut.IsEmpty;

			// Verify
			Assert.That(isEmpty, Is.False, "Queue reports it is empty");
		}

		[Test]
		public void QueuedProjects_ReturnsOldestFirst()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory();
			Directory.CreateDirectory(editQueueDir);
			File.WriteAllText(Path.Combine(editQueueDir, "projb"), string.Empty);

			// wait 1s so that we get a different timestamp on the file
			Thread.Sleep(1000);
			File.WriteAllText(Path.Combine(editQueueDir, "proja"), string.Empty);

			// don't use test double here - we want to test the sorting by date/time
			var sut = new Queue(_env.Settings);

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects.Length, Is.EqualTo(2));
			Assert.That(queuedProjects[0], Is.EqualTo("projb"), "First file");
			Assert.That(queuedProjects[1], Is.EqualTo("proja"), "Second file");
		}

		[Test]
		public void EnqueueProject_Works()
		{
			var sut = Queue.GetQueue();

			// Exercise
			sut.EnqueueProject("foo");

			// Verify
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));

			var queueWithWork = Queue.GetQueue();
			Assert.That(queueWithWork, Is.Not.Null);
			Assert.That(queueWithWork.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));
			var queuedProjectFile = Path.Combine(_env.Settings.GetQueueDirectory(), "foo");
			Assert.That(File.Exists(queuedProjectFile), Is.True);
		}

		[Test]
		public void DequeueProject_Works()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory();
			File.WriteAllText(Path.Combine(editQueueDir, "foo"), string.Empty);
			var sut = Queue.GetQueue();

			// Exercise
			sut.DequeueProject("foo");

			// Verify
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new string[] { }));
			Assert.That(File.Exists(Path.Combine(editQueueDir, "foo")), Is.False);
		}

		[Test]
		public void DequeueProject_NonExistingProject_Ignored()
		{
			var sut = Queue.GetQueue();

			// Exercise/Verify
			Assert.That(() => sut.DequeueProject("foo"), Throws.Nothing);
		}

		[TestCase(ActionNames.Synchronize)]
		public void CurrentAction_Works(ActionNames expectedAction)
		{
			var sut = Queue.GetQueue();

			Assert.That(sut.CurrentActionName, Is.EqualTo(expectedAction));
		}
	}
}
