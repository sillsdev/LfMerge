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
		private ILexEntryRepository entryRepo;
		private ILexExampleSentenceRepository exampleRepo;
		private ICmPictureRepository pictureRepo;
		private ILexSenseRepository senseRepo;
		private ILexEntryFactory entryFactory;
		private ILexExampleSentenceFactory exampleFactory;
		private ICmPictureFactory pictureFactory;
		private ILexSenseFactory senseFactory;

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

			// For efficiency's sake, cache the four repositories and factories we'll need all the time
			entryRepo = servLoc.GetInstance<ILexEntryRepository>();
			exampleRepo = servLoc.GetInstance<ILexExampleSentenceRepository>();
			pictureRepo = servLoc.GetInstance<ICmPictureRepository>();
			senseRepo = servLoc.GetInstance<ILexSenseRepository>();
			entryFactory = servLoc.GetInstance<ILexEntryFactory>();
			exampleFactory = servLoc.GetInstance<ILexExampleSentenceFactory>();
			pictureFactory = servLoc.GetInstance<ICmPictureFactory>();
			senseFactory = servLoc.GetInstance<ILexSenseFactory>();

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
			Guid guid = lfEntry.Guid;
			ILexEntry fdoEntry = GetOrCreateEntryByGuid(guid);
			var result = NonUndoableUnitOfWorkHelper.Do<ILexEntry>(cache.ActionHandlerAccessor, () =>
			{
				// TODO: Set instance fields
				return fdoEntry;
			});
			return result;
		}

		private ILexExampleSentence LfExampleToFdoExample(LfExample lfExample)
		{
			Guid guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence fdoExample = GetOrCreateExampleByGuid(guid);
			var result = NonUndoableUnitOfWorkHelper.Do<ILexExampleSentence>(cache.ActionHandlerAccessor, () =>
			{
				// TODO: Set instance fields
				return fdoExample;
			});
			return result;
		}

		private ICmPicture LfPictureToFdoPicture(LfPicture lfPicture)
		{
			Guid guid = lfPicture.Guid;
			ICmPicture fdoPicture = GetOrCreatePictureByGuid(guid);
			var result = NonUndoableUnitOfWorkHelper.Do<ICmPicture>(cache.ActionHandlerAccessor, () =>
			{
				// TODO: Set instance fields
				return fdoPicture;
			});
			// cache.ActionHandlerAccessor.Commit(); // TODO: Consider whether this belongs here, or whether we should Commit() a lot of things at once.
			return result;
		}

		private ILexSense LfSenseToFdoSense(LfSense lfSense)
		{
			Guid guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense fdoSense = GetOrCreateSenseByGuid(guid);
			var result = NonUndoableUnitOfWorkHelper.Do<ILexSense>(cache.ActionHandlerAccessor, () =>
			{
				// TODO: Set instance fields
				return fdoSense;
			});
			return result;
		}

		private TObject GetOrCreateCmObjectByGuid<TObject>(Guid guid, IRepository<TObject> repo, IFdoFactory<TObject> factory)
			where TObject : ICmObject
		{
			var result = NonUndoableUnitOfWorkHelper.Do<TObject>(cache.ActionHandlerAccessor, () =>
			{
				TObject cmObject;
				if (guid == default(Guid))
				{
					cmObject = factory.Create();
					Console.WriteLine("Created CmObject with GUID {0}", cmObject.Guid);
				}
				else
				{
					cmObject = repo.GetObject(guid);
				}

				return cmObject;
			});
			return result;
		}

		private ILexEntry GetOrCreateEntryByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ILexEntry>(guid, entryRepo, entryFactory);
		}

		private ILexExampleSentence GetOrCreateExampleByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ILexExampleSentence>(guid, exampleRepo, exampleFactory);
		}

		private ICmPicture GetOrCreatePictureByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ICmPicture>(guid, pictureRepo, pictureFactory);
		}

		private ILexSense GetOrCreateSenseByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ILexSense>(guid, senseRepo, senseFactory);
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

