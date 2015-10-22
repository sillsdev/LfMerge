// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Palaso.TestUtilities;
using System.IO;
using LfMerge.Queues;
using NUnit.Framework;
using System.Threading;

namespace LfMerge.Tests.Queues
{
	[TestFixture]
	public class QueueTests
	{
		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			// Force setting of Options.Current
			new Options();
		}

		[Test]
		public void Ctor_CantCreateQueueForNone()
		{
			Assert.That(() => new Queue(QueueNames.None), Throws.ArgumentException);
		}

		[Test]
		public void IsEmpty_NoFilesReturnsEmpty()
		{
			using (var tempDir = new TemporaryFolder("SomeQueue"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				var mergequeueDir = lfDirs.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				var queue = new Queue(QueueNames.Merge);

				// SUT
				var isEmpty = queue.IsEmpty;

				// Verify
				Assert.That(isEmpty, Is.True, "Queue doesn't report it is empty");
			}
		}

		[Test]
		public void IsEmpty_ExistingFilesReturnsNotEmpty()
		{
			using (var tempDir = new TemporaryFolder("SomeQueue"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				var mergequeueDir = lfDirs.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				File.WriteAllText(Path.Combine(mergequeueDir, "proja"), string.Empty);
				var queue = new Queue(QueueNames.Merge);

				// SUT
				var isEmpty = queue.IsEmpty;

				// Verify
				Assert.That(isEmpty, Is.False, "Queue reports it is empty");
			}
		}

		[Test]
		public void QueuedProjects_ReturnsOldestFirst()
		{
			using (var tempDir = new TemporaryFolder("QueuedProjects"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				var mergequeueDir = lfDirs.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				File.WriteAllText(Path.Combine(mergequeueDir, "projb"), string.Empty);
				Thread.Sleep(1000);
				File.WriteAllText(Path.Combine(mergequeueDir, "proja"), string.Empty);
				var queue = new Queue(QueueNames.Merge);

				// SUT
				var queuedProjects = queue.QueuedProjects;

				// Verify
				Assert.That(queuedProjects.Length, Is.EqualTo(2));
				Assert.That(queuedProjects[0], Is.EqualTo("projb"), "First file");
				Assert.That(queuedProjects[1], Is.EqualTo("proja"), "Second file");
			}
		}

		private static void CreateQueueDirectories(LfMergeDirectories lfDirs)
		{
			foreach (QueueNames queueName in Enum.GetValues(typeof(QueueNames)))
			{
				var queueDir = lfDirs.GetQueueDirectory(queueName);
				if (queueDir != null)
					Directory.CreateDirectory(queueDir);
			}
		}

		[Test]
		public void NextQueueWithWork_AllQueuesEmptyReturnsNull()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				CreateQueueDirectories(lfDirs);

				// SUT
				var queue = Queue.NextQueueWithWork(Actions.Commit);

				// Verify
				Assert.That(queue, Is.Null);
			}
		}

		[Test]
		public void NextQueueWithWork_ReturnsNonEmptyQueue()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				CreateQueueDirectories(lfDirs);

				var sendQueueDir = lfDirs.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "projz"), string.Empty);

				// SUT
				var queue = Queue.NextQueueWithWork(Actions.Commit);

				// Verify
				Assert.That(queue, Is.Not.Null);
				Assert.That(queue.Name, Is.EqualTo(QueueNames.Send));
				Assert.That(queue.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_CurrentNonEmptyReturnsCurrent()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				CreateQueueDirectories(lfDirs);

				var sendQueueDir = lfDirs.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "projz"), string.Empty);

				// SUT
				var queue = Queue.NextQueueWithWork(Actions.Send);

				// Verify
				Assert.That(queue, Is.Not.Null);
				Assert.That(queue.Name, Is.EqualTo(QueueNames.Send));
				Assert.That(queue.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_NonEmptyQueueWrapAround()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				CreateQueueDirectories(lfDirs);

				var mergeQueueDir = lfDirs.GetQueueDirectory(QueueNames.Merge);
				File.WriteAllText(Path.Combine(mergeQueueDir, "projz"), string.Empty);

				// SUT
				var queue = Queue.NextQueueWithWork(Actions.Commit);

				// Verify
				Assert.That(queue, Is.Not.Null);
				Assert.That(queue.Name, Is.EqualTo(QueueNames.Merge));
				Assert.That(queue.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_SingleQueueReturnsNullIfCurrentEmpty()
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--queue", "send" });

			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				var lfDirs = new LfMergeDirectories(tempDir.Path);
				CreateQueueDirectories(lfDirs);

				var commitQueueDir = lfDirs.GetQueueDirectory(QueueNames.Commit);
				File.WriteAllText(Path.Combine(commitQueueDir, "projz"), string.Empty);

				// SUT
				var queue = Queue.NextQueueWithWork(Actions.Send);

				// Verify
				Assert.That(queue, Is.Null);
			}
		}

	}
}

