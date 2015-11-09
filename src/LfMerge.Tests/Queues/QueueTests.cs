// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using Palaso.TestUtilities;
using System.IO;
using LfMerge.Queues;
using NUnit.Framework;
using System.Threading;
using LfMerge.Actions;

namespace LfMerge.Tests.Queues
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
			public QueueDouble(QueueNames name): base(name)
			{
			}

			protected override string[] RawQueuedProjects
			{
				get { return ProjectsForTesting; }
			}

			public string[] ProjectsForTesting { get; set; }
		}

		private TestEnvironment _env;

		[SetUp]
		public void FixtureSetup()
		{
			// Force setting of Options.Current
			new Options();
			_env = new TestEnvironment();
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
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
				LfMergeSettings.Initialize(tempDir.Path);
				var mergequeueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				var sut = new Queue(QueueNames.Merge);

				// Exercise
				var isEmpty = sut.IsEmpty;

				// Verify
				Assert.That(isEmpty, Is.True, "Queue doesn't report it is empty");
			}
		}

		[Test]
		public void IsEmpty_ExistingFilesReturnsNotEmpty()
		{
			using (var tempDir = new TemporaryFolder("SomeQueue"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				var mergequeueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				File.WriteAllText(Path.Combine(mergequeueDir, "proja"), string.Empty);
				var sut = new Queue(QueueNames.Merge);

				// Exercise
				var isEmpty = sut.IsEmpty;

				// Verify
				Assert.That(isEmpty, Is.False, "Queue reports it is empty");
			}
		}

		[Test]
		public void QueuedProjects_ReturnsOldestFirst()
		{
			using (var tempDir = new TemporaryFolder("QueuedProjects"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				var mergequeueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Merge);
				Directory.CreateDirectory(mergequeueDir);
				File.WriteAllText(Path.Combine(mergequeueDir, "projb"), string.Empty);

				// wait 1s so that we get a different timestamp on the file
				Thread.Sleep(1000);
				File.WriteAllText(Path.Combine(mergequeueDir, "proja"), string.Empty);

				// don't use test double here - we want to test the sorting by date/time
				var sut = new Queue(QueueNames.Merge);

				// Exercise
				var queuedProjects = sut.QueuedProjects;

				// Verify
				Assert.That(queuedProjects.Length, Is.EqualTo(2));
				Assert.That(queuedProjects[0], Is.EqualTo("projb"), "First file");
				Assert.That(queuedProjects[1], Is.EqualTo("proja"), "Second file");
			}
		}

		[TestCase("proja", new[] { "proja", "projd", "projc", "projb"})]
		[TestCase("projb", new[] { "projb", "proja", "projd", "projc"})]
		[TestCase("projc", new[] { "projc", "projb", "proja", "projd"})]
		[TestCase("projd", new[] { "projd", "projc", "projb", "proja"})]
		public void QueuedProjects_PriorityProjectReturnsPriorityBeforeNext(string prioProj,
			string[] expectedProjects)
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--priority-project", prioProj });

			// we use the test double here so that we don't have to wait between creating files
			var sut = new QueueDouble(QueueNames.Merge);
			sut.ProjectsForTesting = new[] { "projc", "projb", "proja", "projd" };

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects, Is.EquivalentTo(expectedProjects));
		}

		[TestCase("proja")]
		[TestCase("projb")]
		[TestCase("projc")]
		[TestCase("projd")]
		public void QueuedProjects_SingleProjectReturnsOnlySingleProj(string singleProj)
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--project", singleProj });

			// we use the test double here so that we don't have to wait between creating files
			var sut = new QueueDouble(QueueNames.Merge);
			sut.ProjectsForTesting = new[] { "projc", "projb", "proja", "projd" };

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects, Is.EquivalentTo(new[] { singleProj }));
		}

		// test single project if that project has nothing in queue
		[Test]
		public void QueuedProjects_SingleProjectEmptyReturnsEmpty()
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--project", "proja" });

			// we use the test double here so that we don't have to wait between creating files
			var sut = new QueueDouble(QueueNames.Merge);
			sut.ProjectsForTesting = new[] { "projc", "projb", "projd" };

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects, Is.EquivalentTo(new string[] { }));
		}

		[Test]
		public void NextQueueWithWork_AllQueuesEmptyReturnsNull()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				// Exercise
				var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

				// Verify
				Assert.That(sut, Is.Null);
			}
		}

		[Test]
		public void NextQueueWithWork_ReturnsNonEmptyQueue()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var sendQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "projz"), string.Empty);

				// Exercise
				var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

				// Verify
				Assert.That(sut, Is.Not.Null);
				Assert.That(sut.Name, Is.EqualTo(QueueNames.Send));
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_CurrentNonEmptyReturnsCurrent()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var sendQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "projz"), string.Empty);

				// Exercise
				var sut = Queue.GetNextQueueWithWork(ActionNames.Send);

				// Verify
				Assert.That(sut, Is.Not.Null);
				Assert.That(sut.Name, Is.EqualTo(QueueNames.Send));
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_NonEmptyQueueWrapAround()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var mergeQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Merge);
				File.WriteAllText(Path.Combine(mergeQueueDir, "projz"), string.Empty);

				// Exercise
				var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

				// Verify
				Assert.That(sut, Is.Not.Null);
				Assert.That(sut.Name, Is.EqualTo(QueueNames.Merge));
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void NextQueueWithWork_SingleQueueReturnsNullIfCurrentEmpty()
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--queue", "send" });

			using (var tempDir = new TemporaryFolder("NextQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var commitQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Commit);
				File.WriteAllText(Path.Combine(commitQueueDir, "projz"), string.Empty);

				// Exercise
				var sut = Queue.GetNextQueueWithWork(ActionNames.Send);

				// Verify
				Assert.That(sut, Is.Null);
			}
		}

		[Test]
		public void FirstQueueWithWork_AllQueuesEmptyReturnsNull()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("FirstQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				// Exercise
				var sut = Queue.FirstQueueWithWork;

				// Verify
				Assert.That(sut, Is.Null);
			}
		}

		[Test]
		public void FirstQueueWithWork_ReturnsNonEmptyQueue()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("FirstQueueWithWork"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var sendQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "projz"), string.Empty);

				// Exercise
				var sut = Queue.FirstQueueWithWork;

				// Verify
				Assert.That(sut, Is.Not.Null);
				Assert.That(sut.Name, Is.EqualTo(QueueNames.Send));
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
			}
		}

		[Test]
		public void EnqueueProject_Works()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("EnqueueProject"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();
				var sut = Queue.GetQueue(QueueNames.Commit);

				// Exercise
				sut.EnqueueProject("foo");

				// Verify
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));

				var queueWithWork = Queue.FirstQueueWithWork;
				Assert.That(queueWithWork, Is.Not.Null);
				Assert.That(queueWithWork.Name, Is.EqualTo(QueueNames.Commit));
				Assert.That(queueWithWork.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));
				var queuedProjectFile = Path.Combine(LfMergeSettings.Current.GetQueueDirectory(QueueNames.Commit), "foo");
				Assert.That(File.Exists(queuedProjectFile), Is.True);
			}
		}

		[Test]
		public void DequeueProject_Works()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("DequeueProject"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var sendQueueDir = LfMergeSettings.Current.GetQueueDirectory(QueueNames.Send);
				File.WriteAllText(Path.Combine(sendQueueDir, "foo"), string.Empty);
				var sut = Queue.GetQueue(QueueNames.Send);

				// Exercise
				sut.DequeueProject("foo");

				// Verify
				Assert.That(sut.QueuedProjects, Is.EquivalentTo(new string[] { }));
				Assert.That(File.Exists(Path.Combine(sendQueueDir, "foo")), Is.False);
			}
		}

		[Test]
		public void DequeueProject_NonExistingProjectIgnored()
		{
			// Setup
			using (var tempDir = new TemporaryFolder("DequeueProject"))
			{
				LfMergeSettings.Initialize(tempDir.Path);
				Queue.CreateQueueDirectories();

				var sut = Queue.GetQueue(QueueNames.Receive);

				// Exercise/Verify
				Assert.That(() => sut.DequeueProject("foo"), Throws.Nothing);
			}
		}

		[TestCase(QueueNames.Merge, typeof(UpdateFdoFromMongoDbAction))]
		[TestCase(QueueNames.Commit, typeof(CommitAction))]
		[TestCase(QueueNames.Receive, typeof(ReceiveAction))]
		[TestCase(QueueNames.Send, typeof(SendAction))]
		public void CurrentAction_Works(QueueNames queueName, Type expectedType)
		{
			var sut = Queue.GetQueue(queueName);

			Assert.That(sut.CurrentAction, Is.TypeOf(expectedType));
		}
	}
}

