// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Actions
{
	public class UpdateFdoFromMongoDbAction: Action
	{
		private FdoCache cache;

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.QUEUED; }
		}

		protected override void DoRun(ILfProject project)
		{
			ILfProjectConfig config = GetConfigForTesting(project);
			if (config == null)
				return;

			// TODO: Remove this block of debugging output code soonish.
			IEnumerable<LfLexEntry> lexicon = GetLexiconForTesting(project, config);
			foreach (LfLexEntry entry in lexicon)
			{
				Console.Write("{0}: ", entry.Guid);
				if (entry.Lexeme != null && entry.Lexeme.Values != null)
					Console.WriteLine(String.Join(", ", entry.Lexeme.Values.Select(x => (x.Value == null) ? "" : x.Value)));
				foreach(LfSense sense in entry.Senses)
				{
					if (sense.PartOfSpeech != null)
					{
						if (sense.PartOfSpeech.Value != null)
							Console.Write(" - " + sense.PartOfSpeech.Value);
					}
					Console.WriteLine();
				}
			}

			if (project.FieldWorksProject == null)
			{
				Console.WriteLine("Failed to find the corresponding FieldWorks project!");
				return;
			}
			cache = project.FieldWorksProject.Cache;
			if (cache == null)
			{
				Console.WriteLine("Failed to find the FDO cache!");
				var fwProject = project.FieldWorksProject;
				Console.WriteLine(fwProject.IsDisposed);
				return;
			}

			IFdoServiceLocator servLoc = cache.ServiceLocator;
			Console.WriteLine("Got the service locator");
			ILexSenseRepository senseRepo = servLoc.GetInstance<ILexSenseRepository>();
			Console.WriteLine("Got the sense repository");

			var emptyPicture = new LfPicture();
			Console.WriteLine("Empty picture has GUID {0}", emptyPicture.Guid);
			ICmPicture fdoPicture = LfPictureToFdoPicture(emptyPicture);
			Console.WriteLine("FDO picture has GUID {0}", fdoPicture.Guid);
			return; // Stop now for debugging purposes

			lexicon = GetLexiconForTesting(project, config);
			foreach (LfLexEntry entry in lexicon)
			{
				string entryName = String.Join(", ", entry.Lexeme.Values.Select(x => (x.Value == null) ? "" : x.Value));
				Console.WriteLine("Checking entry {0} in lexicon", entryName);
				if (entry.Senses == null) continue;
				foreach(LfSense sense in entry.Senses)
				{
					if (sense.Definition == null)
					{
						Console.WriteLine("No definition found in LF entry");
						continue;
					}
					string senseName = String.Join(", ", sense.Definition.Values.Select(x => (x.Value == null) ? "" : x.Value));
					Console.WriteLine("Checking sense {0} in entry {1} in lexicon", senseName, entryName);
					Guid liftId;
					ILexSense fwSense = null;
					if (Guid.TryParse(sense.LiftId, out liftId))
						fwSense = senseRepo.GetObject(liftId);
					// TODO: Handle GUID parsing failures
					if (fwSense == null)
					{
						Console.WriteLine("Didn't find sense {0} in FDO", sense.LiftId);
						continue;
					}
					IMultiString definition = fwSense.Definition;
					if (definition == null)
					{
						Console.WriteLine("Didn't find definition for {0} in FDO", sense.LiftId);
						continue;
					}

					Console.WriteLine("Definition: {0}", definition.BestAnalysisVernacularAlternative.Text);
					foreach (int wsid in definition.AvailableWritingSystemIds)
					{
						string wsid_str = servLoc.WritingSystemManager.GetStrFromWs(wsid);
						var text = definition.get_String(wsid);
						Console.WriteLine("{0}: {1}", wsid_str, text.Text);
					}
				}
			}
		}

		private IEnumerable<LfLexEntry> GetLexiconForTesting(ILfProject project, ILfProjectConfig config)
		{
			var db = MongoConnection.Default.GetProjectDatabase(project);
			var collection = db.GetCollection<LfLexEntry>("lexicon");
			IAsyncCursor<LfLexEntry> result2 = collection.Find<LfLexEntry>(_ => true).ToCursorAsync().Result;
			return result2.AsEnumerable();
		}

		private ILfProjectConfig GetConfigForTesting(ILfProject project)
		{
			MongoProjectRecord projectRecord = MongoProjectRecord.Create(project);
			if (projectRecord == null)
			{
				Console.WriteLine("No project named {0}", project.LfProjectCode);
				Console.WriteLine("If we are unit testing, this may not be an error");
				return null;
			}
			ILfProjectConfig config = projectRecord.Config;
			Console.WriteLine(config.GetType()); // Should be LfMerge.LanguageForge.Config.LfProjectConfig
			Console.WriteLine(config.Entry.Type);
			Console.WriteLine(String.Join(", ", config.Entry.FieldOrder));
			Console.WriteLine(config.Entry.Fields["lexeme"].Type);
			Console.WriteLine(config.Entry.Fields["lexeme"].GetType());
			return config;
		}

		private Guid GuidFromLiftId(string LiftId)
		{
			Guid result;
			if (String.IsNullOrEmpty(LiftId))
				return default(Guid);
			if (Guid.TryParse(LiftId, out result))
				return result;
			int pos = LiftId.LastIndexOf('_');
			if (Guid.TryParse(LiftId.Substring(pos+1), out result))
				return result;
			return default(Guid);
		}

		private ILexEntry LfLexEntryToFdoLexEntry(LfLexEntry lfEntry)
		{
			var repo = cache.ServiceLocator.GetInstance<ILexEntryRepository>();
			var factory = cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			ILexEntry fdoEntry;
			Guid guid = lfEntry.Guid;
			if (guid == default(Guid))
			{
				fdoEntry = factory.Create();
				Console.WriteLine("Created fdoEntry with GUID {0}", fdoEntry.Guid);
			}
			else
			{
				fdoEntry = repo.GetObject(guid);
			}

			return fdoEntry;
		}

		private ILexExampleSentence LfExampleToFdoExample(LfExample lfExample)
		{
			var repo = cache.ServiceLocator.GetInstance<ILexExampleSentenceRepository>();
			var factory = cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>();
			ILexExampleSentence fdoExample;
			Guid guid = GuidFromLiftId(lfExample.LiftId);
			if (guid == default(Guid))
			{
				// TODO: Figure out how to create a new object.
				fdoExample = factory.Create();
				Console.WriteLine("Created fdoExample with GUID {0}", fdoExample.Guid);
				return null;
			}
			fdoExample = repo.GetObject(guid);

			return fdoExample;
		}

		private ICmPicture LfPictureToFdoPicture(LfPicture lfPicture)
		{
			cache.ActionHandlerAccessor.BeginNonUndoableTask();
			// Another way of doing it would be:
			// using (var helper = NonUndoableUnitOfWorkHelper())
			// ; // Rest of function would go here
			var repo = cache.ServiceLocator.GetInstance<ICmPictureRepository>();
			var factory = cache.ServiceLocator.GetInstance<ICmPictureFactory>();
			ICmPicture fdoPicture;
			Guid guid = lfPicture.Guid;
			if (guid == default(Guid))
			{
				Console.WriteLine("About to create fdoPicture, will print GUID after creation");
				fdoPicture = factory.Create();
				Console.WriteLine("Created fdoPicture with GUID {0}", fdoPicture.Guid);
			}
			else
			{
				fdoPicture = repo.GetObject(guid);
			}
			cache.ActionHandlerAccessor.EndNonUndoableTask();
			cache.ActionHandlerAccessor.Commit();
			return fdoPicture;
		}

		private ILexSense LfSenseToFdoSense(LfSense lfSense)
		{
			var repo = cache.ServiceLocator.GetInstance<ILexSenseRepository>();
			var factory = cache.ServiceLocator.GetInstance<ILexSenseFactory>();
			Guid guid = GuidFromLiftId(lfSense.LiftId);
			if (guid == default(Guid))
			{
				// TODO: Try another approach for finding the object before giving up and returning null?
				return null;
			}
			ILexSense fdoSense = repo.GetObject(guid);

			return fdoSense;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

