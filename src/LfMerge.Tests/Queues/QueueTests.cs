// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using LfMerge.Actions;
using LfMerge.Queues;
using LfMerge.Settings;
using Palaso.TestUtilities;
using NUnit.Framework;

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
			public QueueDouble(LfMergeSettingsIni settings, QueueNames name): base(settings, name)
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
		[ExpectedException( "System.ArgumentException" )]
		public void Ctor_CantCreateQueueForNone()
		{
			new Queue(_env.Settings, QueueNames.None);
		}

		[Test]
		public void IsEmpty_NoFiles_ReturnsEmpty()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Edit);
			Directory.CreateDirectory(editQueueDir);
			var sut = new Queue(_env.Settings, QueueNames.Edit);

			// Exercise
			var isEmpty = sut.IsEmpty;

			// Verify
			Assert.That(isEmpty, Is.True, "Queue doesn't report it is empty");
		}

		[Test]
		public void IsEmpty_ExistingFiles_ReturnsNotEmpty()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Edit);
			Directory.CreateDirectory(editQueueDir);
			File.WriteAllText(Path.Combine(editQueueDir, "proja"), string.Empty);
			var sut = new Queue(_env.Settings, QueueNames.Edit);

			// Exercise
			var isEmpty = sut.IsEmpty;

			// Verify
			Assert.That(isEmpty, Is.False, "Queue reports it is empty");
		}

		[Test]
		public void QueuedProjects_ReturnsOldestFirst()
		{
			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Edit);
			Directory.CreateDirectory(editQueueDir);
			File.WriteAllText(Path.Combine(editQueueDir, "projb"), string.Empty);

			// wait 1s so that we get a different timestamp on the file
			Thread.Sleep(1000);
			File.WriteAllText(Path.Combine(editQueueDir, "proja"), string.Empty);

			// don't use test double here - we want to test the sorting by date/time
			var sut = new Queue(_env.Settings, QueueNames.Edit);

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects.Length, Is.EqualTo(2));
			Assert.That(queuedProjects[0], Is.EqualTo("projb"), "First file");
			Assert.That(queuedProjects[1], Is.EqualTo("proja"), "Second file");
		}

		[TestCase("proja", new[] { "proja", "projd", "projc", "projb"})]
		[TestCase("projb", new[] { "projb", "proja", "projd", "projc"})]
		[TestCase("projc", new[] { "projc", "projb", "proja", "projd"})]
		[TestCase("projd", new[] { "projd", "projc", "projb", "proja"})]
		public void QueuedProjects_PriorityProject_ReturnsPriorityBeforeNext(string prioProj,
			string[] expectedProjects)
		{
			// Setup
			Options.ParseCommandLineArgs(new[] { "--project", prioProj });

			// we use the test double here so that we don't have to wait between creating files
			var sut = new QueueDouble(_env.Settings, QueueNames.Edit);
			sut.ProjectsForTesting = new[] { "projc", "projb", "proja", "projd" };

			// Exercise
			var queuedProjects = sut.QueuedProjects;

			// Verify
			Assert.That(queuedProjects, Is.EquivalentTo(expectedProjects));
		}

		[Test]
		public void NextQueueWithWork_AllQueuesEmpty_ReturnsNull()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			// Exercise
			var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

			// Verify
			Assert.That(sut, Is.Null);
		}

		[Test]
		public void NextQueueWithWork_ReturnsNonEmptyQueue()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var syncQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Synchronize);
			File.WriteAllText(Path.Combine(syncQueueDir, "projz"), string.Empty);

			// Exercise
			var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

			// Verify
			Assert.That(sut, Is.Not.Null);
			Assert.That(sut.Name, Is.EqualTo(QueueNames.Synchronize));
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
		}

		[Test]
		public void NextQueueWithWork_CurrentNonEmpty_ReturnsCurrent()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Synchronize);
			File.WriteAllText(Path.Combine(editQueueDir, "projz"), string.Empty);

			// Exercise
			var sut = Queue.GetNextQueueWithWork(ActionNames.Synchronize);

			// Verify
			Assert.That(sut, Is.Not.Null);
			Assert.That(sut.Name, Is.EqualTo(QueueNames.Synchronize));
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
		}

		[Test]
		public void NextQueueWithWork_NonEmptyQueue_WrapAround()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Synchronize);
			File.WriteAllText(Path.Combine(editQueueDir, "projz"), string.Empty);

			// Exercise
			var sut = Queue.GetNextQueueWithWork(ActionNames.Commit);

			// Verify
			Assert.That(sut, Is.Not.Null);
			Assert.That(sut.Name, Is.EqualTo(QueueNames.Synchronize));
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
		}

		[Test]
		public void FirstQueueWithWork_AllQueuesEmpty_ReturnsNull()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			// Exercise
			var sut = Queue.FirstQueueWithWork;

			// Verify
			Assert.That(sut, Is.Null);
		}

		[Test]
		public void FirstQueueWithWork_ReturnsNonEmptyQueue()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Synchronize);
			File.WriteAllText(Path.Combine(editQueueDir, "projz"), string.Empty);

			// Exercise
			var sut = Queue.FirstQueueWithWork;

			// Verify
			Assert.That(sut, Is.Not.Null);
			Assert.That(sut.Name, Is.EqualTo(QueueNames.Synchronize));
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "projz"}));
		}

		[Test]
		public void EnqueueProject_Works()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);
			var sut = Queue.GetQueue(QueueNames.Synchronize);

			// Exercise
			sut.EnqueueProject("foo");

			// Verify
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));

			var queueWithWork = Queue.FirstQueueWithWork;
			Assert.That(queueWithWork, Is.Not.Null);
			Assert.That(queueWithWork.Name, Is.EqualTo(QueueNames.Synchronize));
			Assert.That(queueWithWork.QueuedProjects, Is.EquivalentTo(new[] { "foo"}));
			var queuedProjectFile = Path.Combine(_env.Settings.GetQueueDirectory(QueueNames.Synchronize), "foo");
			Assert.That(File.Exists(queuedProjectFile), Is.True);
		}

		[Test]
		public void DequeueProject_Works()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var editQueueDir = _env.Settings.GetQueueDirectory(QueueNames.Edit);
			File.WriteAllText(Path.Combine(editQueueDir, "foo"), string.Empty);
			var sut = Queue.GetQueue(QueueNames.Edit);

			// Exercise
			sut.DequeueProject("foo");

			// Verify
			Assert.That(sut.QueuedProjects, Is.EquivalentTo(new string[] { }));
			Assert.That(File.Exists(Path.Combine(editQueueDir, "foo")), Is.False);
		}

		[Test]
		public void DequeueProject_NonExistingProject_Ignored()
		{
			// Setup
			Queue.CreateQueueDirectories(_env.Settings);

			var sut = Queue.GetQueue(QueueNames.Synchronize);

			// Exercise/Verify
			Assert.That(() => sut.DequeueProject("foo"), Throws.Nothing);
		}

		[TestCase(QueueNames.Edit, typeof(EditAction))]
		[TestCase(QueueNames.Synchronize, typeof(SynchronizeAction))]
		public void CurrentAction_Works(QueueNames queueName, Type expectedType)
		{
			var sut = Queue.GetQueue(queueName);

			Assert.That(sut.CurrentAction, Is.TypeOf(expectedType));
		}
	}
}
