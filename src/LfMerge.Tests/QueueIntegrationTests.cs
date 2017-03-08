// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using NUnit.Framework;
using LfMerge.Core.Tests;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;

namespace LfMerge.Tests
{
	[TestFixture]
	public class QueueIntegrationTests
	{
		/// <summary>
		/// Test double for Queue class. This test double allows to set the list of projects.
		/// This simulates getting the list of files sorted by time and saves the time we'd have
		/// to wait between creating the different files.
		/// </summary>
		private class QueueDouble: Queue
		{
			public QueueDouble(LfMergeSettings settings, QueueNames name): base(settings, name)
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
		public void Setup()
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

	}
}

