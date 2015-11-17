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
		private IFdoServiceLocator servLoc;
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

			if (project.FieldWorksProject == null)
			{
				Console.WriteLine("Failed to find the corresponding FieldWorks project!");
				return;
			}
			Console.WriteLine("Project {0} disposed", project.FieldWorksProject.IsDisposed ? "is" : "is not");
			cache = project.FieldWorksProject.Cache;
			if (cache == null)
			{
				Console.WriteLine("Failed to find the FDO cache!");
				var fwProject = project.FieldWorksProject;
				return;
			}

			servLoc = cache.ServiceLocator;
			if (servLoc == null)
			{
				Console.WriteLine("Failed to find the service locator; giving up.");
				return;
			}

			// For efficiency's sake, cache the four repositories and factories we'll need all the time
			entryRepo = servLoc.GetInstance<ILexEntryRepository>();
			exampleRepo = servLoc.GetInstance<ILexExampleSentenceRepository>();
			pictureRepo = servLoc.GetInstance<ICmPictureRepository>();
			senseRepo = servLoc.GetInstance<ILexSenseRepository>();
			entryFactory = servLoc.GetInstance<ILexEntryFactory>();
			exampleFactory = servLoc.GetInstance<ILexExampleSentenceFactory>();
			pictureFactory = servLoc.GetInstance<ICmPictureFactory>();
			senseFactory = servLoc.GetInstance<ILexSenseFactory>();

			IEnumerable<LfLexEntry> lexicon = GetLexiconForTesting(project, config);
			NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
			{
				foreach (LfLexEntry lfEntry in lexicon)
				{
					#pragma warning disable 0219 // "Variable is assigned but its value is never used"
					ILexEntry fdoEntry = LfLexEntryToFdoLexEntry(lfEntry);
					#pragma warning restore 0219
				}
			});
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

		/// <summary>
		/// Sets all writing systems in an FDO multi string from a LanguageForge MultiText field.
		/// Destination is first parameter, like the order of an assignment statement.
		/// </summary>
		/// <param name="dest">FDO multi string whose values will be set.</param>
		/// <param name="source">Source of multistring values.</param>
		private void SetMultiStringFrom(IMultiStringAccessor dest, LfMultiText source)
		{
			if (source != null)
				source.WriteToFdoMultiString(dest, servLoc.WritingSystemManager);
		}

		private ILexEntry LfLexEntryToFdoLexEntry(LfLexEntry lfEntry)
		{
			Guid guid = lfEntry.Guid;
			ILexEntry fdoEntry = GetOrCreateEntryByGuid(guid);
			// TODO: Set instance fields
			string entryNameForDebugging = String.Join(", ", lfEntry.Lexeme.Values.Select(x => (x.Value == null) ? "" : x.Value));
			Console.WriteLine("Checking entry {0} ({1}) in lexicon", guid, entryNameForDebugging);
			if (lfEntry.Senses != null) {
				foreach(LfSense lfSense in lfEntry.Senses)
				{
					ILexSense fdoSense = LfSenseToFdoSense(lfSense, fdoEntry);
					if (fdoSense.Owner == null)
						Console.WriteLine("Oh dear, created an fdoSense without an owner!");
						// fdoEntry.SensesOS.Add(fdoSense); // TODO: Verify that this correctly sets up ownership

					fdoEntry.DateCreated = lfEntry.AuthorInfo.CreatedDate;
					fdoEntry.DateModified = lfEntry.AuthorInfo.ModifiedDate;
					// TODO: What about lfEntry.DateCreated and lfEntry.DateModified?

					SetMultiStringFrom(fdoEntry.Bibliography, lfEntry.EntryBibliography);

					/* LfLexEntry fields not mapped:
					lfEntry.CitationForm // Read-only virtual field
					lfEntry.CustomFields // TODO: Handle later
					lfEntry.CvPattern // No FDO equivalent? That can't be right. TODO: Find FDO equivalent.

					*/
				}
			}
			return fdoEntry;
		}

		private ILexExampleSentence LfExampleToFdoExample(LfExample lfExample, ILexSense owner)
		{
			Guid guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence fdoExample = GetOrCreateExampleByGuid(guid, owner);
			// TODO: Set instance fields
			return fdoExample;
		}

		private ICmPicture LfPictureToFdoPicture(LfPicture lfPicture)
		{
			Guid guid = lfPicture.Guid;
			ICmPicture fdoPicture = GetOrCreatePictureByGuid(guid);
			// TODO: Set instance fields
			return fdoPicture;
		}

		private ILexSense LfSenseToFdoSense(LfSense lfSense, ILexEntry owner)
		{
			Guid guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense fdoSense = GetOrCreateSenseByGuid(guid, owner);
			// TODO: Set instance fields
			string senseName;
			if (lfSense.Definition == null)
			{
				Console.WriteLine("No definition found in LF entry for {0}", lfSense.LiftId);
				senseName = "(unknown definition)";
			}
			else
			{
				senseName = String.Join(", ", lfSense.Definition.Values.Select(x => (x.Value == null) ? "" : x.Value));
			}
			Console.WriteLine("Checking sense {0}", senseName);

			if (fdoSense.Cache == null)
				Console.WriteLine("fdoSense.Cache is null. Might cause problems.");
			if (fdoSense.Cache.TsStrFactory == null)
				Console.WriteLine("fdoSense.Cache.TsStrFactory is null. Might cause problems.");
			SetMultiStringFrom(fdoSense.Definition, lfSense.Definition);

			IMultiString definition = fdoSense.Definition;
			// We check for Guid.Empty because a newly-created fdoSense has a Definition that isn't null, but
			// that can't fetch a BestAnalysisVernacularAlternative property.
			if (definition == null || fdoSense.Guid == Guid.Empty || definition.BestAnalysisVernacularAlternative == null)
			{
				Console.WriteLine("Didn't find definition for {0} in FDO", lfSense.LiftId);
				return fdoSense;
			}

			Console.WriteLine("Definition: {0}", definition.BestAnalysisVernacularAlternative.Text);
			foreach (int wsid in definition.AvailableWritingSystemIds)
			{
				string wsid_str = servLoc.WritingSystemManager.GetStrFromWs(wsid);
				var text = definition.get_String(wsid);
				Console.WriteLine("{0}: {1}", wsid_str, text.Text);
			}
			return fdoSense;
		}

		private TObject GetOrCreateCmObjectByGuid<TObject, TOwner>(Guid guid, TOwner owner, IRepository<TObject> repo, Func<TObject> factoryFuncNoOwner, Func<Guid, TOwner, TObject> factoryFuncWithOwner)
			where TObject : ICmObject
		{
			TObject cmObject;
			if (repo.TryGetObject(guid, out cmObject))
			{
				return cmObject;
			}
			else
			{
				if (owner == null)
					cmObject = factoryFuncNoOwner();
				else
					cmObject = factoryFuncWithOwner(guid, owner);
				return cmObject;
			}
		}

		private ILexEntry GetOrCreateEntryByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ILexEntry, ICmObject>(guid, null, entryRepo, entryFactory.Create, null);
		}

		private ILexExampleSentence GetOrCreateExampleByGuid(Guid guid, ILexSense owner)
		{
			return GetOrCreateCmObjectByGuid<ILexExampleSentence, ILexSense>(guid, owner, exampleRepo, null, exampleFactory.Create);
		}

		private ICmPicture GetOrCreatePictureByGuid(Guid guid)
		{
			return GetOrCreateCmObjectByGuid<ICmPicture, ICmObject>(guid, null, pictureRepo, pictureFactory.Create, null);
		}

		private ILexSense GetOrCreateSenseByGuid(Guid guid, ILexEntry owner)
		{
			return GetOrCreateCmObjectByGuid<ILexSense, ILexEntry>(guid, owner, senseRepo, null, senseFactory.Create);
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

