// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using SIL.CoreImpl;
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
		private ILexPronunciationRepository pronunciationRepo;
		private ILexSenseRepository senseRepo;
		private ILexEntryFactory entryFactory;
		private ILexExampleSentenceFactory exampleFactory;
		private ICmPictureFactory pictureFactory;
		private ILexPronunciationFactory pronunciationFactory;
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

			// For efficiency's sake, cache the five repositories and five factories we'll need all the time,
			entryRepo = servLoc.GetInstance<ILexEntryRepository>();
			exampleRepo = servLoc.GetInstance<ILexExampleSentenceRepository>();
			pictureRepo = servLoc.GetInstance<ICmPictureRepository>();
			pronunciationRepo = servLoc.GetInstance<ILexPronunciationRepository>();
			senseRepo = servLoc.GetInstance<ILexSenseRepository>();
			entryFactory = servLoc.GetInstance<ILexEntryFactory>();
			exampleFactory = servLoc.GetInstance<ILexExampleSentenceFactory>();
			pictureFactory = servLoc.GetInstance<ICmPictureFactory>();
			pronunciationFactory = servLoc.GetInstance<ILexPronunciationFactory>();
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
			if (lfEntry.IsDeleted)
			{
				if (fdoEntry.CanDelete)
					fdoEntry.Delete();
				else
					// TODO: Figure out how to handle this situation
					Console.WriteLine("Problem: need to delete an FDO entry, but its CanDelete flag is false.");
			}
			// TODO: Set instance fields
			string entryNameForDebugging = String.Join(", ", lfEntry.Lexeme.Values.Select(x => (x.Value == null) ? "" : x.Value));
			Console.WriteLine("Checking entry {0} ({1}) in lexicon", guid, entryNameForDebugging);

			// Fields in order by lfEntry property, except for Senses and CustomFields, which are handled at the end
			SetMultiStringFrom(fdoEntry.CitationForm, lfEntry.CitationForm);
			fdoEntry.DateCreated = lfEntry.DateCreated;
			fdoEntry.DateModified = lfEntry.DateModified;
			// TODO: What about lfEntry.AuthorInfo? It has CreatedDate and ModifiedDate; What do we do with them?
			SetMultiStringFrom(fdoEntry.Bibliography, lfEntry.EntryBibliography);
			SetMultiStringFrom(fdoEntry.Restrictions, lfEntry.EntryRestrictions);
			SetEtymologyFields(fdoEntry, lfEntry);
			SetLexeme(fdoEntry, lfEntry);
			// fdoEntry.LIFTid = lfEntry.LiftId; // TODO: Figure out how to handle this one.
			SetMultiStringFrom(fdoEntry.LiteralMeaning, lfEntry.LiteralMeaning);
			SetMultiStringFrom(fdoEntry.Comment, lfEntry.Note);
			SetPronunciation(fdoEntry, lfEntry);
			SetMultiStringFrom(fdoEntry.SummaryDefinition, lfEntry.SummaryDefinition);


			/* TODO: Process the following fields too

					fdoEntry.PronunciationsOS.First();
					lfEntry.Environments;
			*/
			/* LfLexEntry fields not mapped:
			lfEntry.CustomFields // TODO: Handle later
			lfEntry.Environments // Don't know how to handle this one. TODO: Research it.
			lfEntry.LiftId // TODO: Figure out how to handle this one. In fdoEntry, it's a constructed value.
			lfEntry.MercurialSha; // TODO: Figure out what this is for
			lfEntry.MorphologyType; // TODO: Put this in fdoEntry.PrimaryMorphType

			*/

			if (lfEntry.Senses != null) {
				foreach(LfSense lfSense in lfEntry.Senses)
				{
					ILexSense fdoSense = LfSenseToFdoSense(lfSense, fdoEntry);
					if (fdoSense.Owner == null)
						Console.WriteLine("Oh dear, created an fdoSense without an owner!");
						// fdoEntry.SensesOS.Add(fdoSense); // TODO: Verify that this correctly sets up ownership

				}
			}
			return fdoEntry;
		}

		private void SetEtymologyFields(ILexEntry fdoEntry, LfLexEntry lfEntry)
		{
			if (lfEntry.Etymology == null &&
			    lfEntry.EtymologyComment == null &&
			    lfEntry.EtymologyGloss == null &&
			    lfEntry.EtymologySource == null)
				return; // Don't create an Etymology object if there's nothing to assign
			ILexEtymology fdoEtymology = fdoEntry.EtymologyOA;
			if (fdoEtymology == null)
				fdoEtymology = CreateOwnedEtymology(fdoEntry); // Also sets owning field on fdoEntry
			SetMultiStringFrom(fdoEtymology.Form, lfEntry.Etymology);
			SetMultiStringFrom(fdoEtymology.Comment, lfEntry.EtymologyComment);
			SetMultiStringFrom(fdoEtymology.Gloss, lfEntry.EtymologyGloss);
			if (lfEntry.EtymologySource != null)
				fdoEtymology.Source = lfEntry.EtymologySource.FirstNonEmptyString(); // TODO: Use best analysis or vernacular instead of just first non-blank entry.
		}

		private void SetLexeme(ILexEntry fdoEntry, LfLexEntry lfEntry)
		{
			if (lfEntry.Lexeme == null)
				return;
			IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
			if (fdoLexeme == null)
				fdoLexeme = CreateOwnedLexemeForm(fdoEntry, lfEntry.MorphologyType); // Also sets owning field on fdoEntry
			SetMultiStringFrom(fdoLexeme.Form, lfEntry.Lexeme);
		}

		private void SetPronunciation(ILexEntry fdoEntry, LfLexEntry lfEntry)
		{
			if (lfEntry.Pronunciation == null &&
			    lfEntry.CvPattern == null &&
			    lfEntry.Tone == null &&
			    lfEntry.Location == null)
			{
				Console.WriteLine("No pronunciation data in lfEntry {0}", lfEntry.Guid);
				return;
			}
			// TODO: Once LF stores pronunciation GUIDs in Mongo, switch to a GetOrCreatePronunciationByGuid method
			ILexPronunciation fdoPronunciation = GetOrCreatePronunciationByGuid(lfEntry.PronunciationGuid, fdoEntry);

			fdoPronunciation.CVPattern = BestStringFromMultiText(lfEntry.CvPattern);
			fdoPronunciation.Tone = BestStringFromMultiText(lfEntry.Tone);
			SetMultiStringFrom(fdoPronunciation.Form, lfEntry.Pronunciation);
			if (lfEntry.Location != null)
			{
				var converter = new PossibilityListConverter(cache.LanguageProject.LocationsOA);
				fdoPronunciation.LocationRA = (ICmLocation)converter.GetByName(lfEntry.Location.Value);
			}
			// Not handling fdoPronunciation.MediaFilesOS. TODO: At some point we may want to handle media files as well.
			// Not handling fdoPronunciation.LiftResidue
		}

		private ITsString BestStringFromMultiText(LfMultiText input)
		{
			if (input == null) return null;
			if (input.Count == 0)
			{
				Console.WriteLine("non-null input, but no contents in it!");
				return null;
			}
			Console.WriteLine("BestStringFromMultiText got a non-null input");
			var wsm = cache.ServiceLocator.WritingSystemManager;
			int wsId = cache.DefaultAnalWs;
			// IWritingSystem en = wsm.Get("en");
			var wsStr = wsm.GetStrFromWs(wsId);
			LfStringField valueField;
			string value;
			if (input.TryGetValue(wsStr, out valueField))
			{
				Console.WriteLine("Returning TsString from {0} for writing system {1}", valueField.Value, wsStr);
				return TsStringUtils.MakeTss(valueField.Value, wsId);
			}
			else
			{
				// TODO: Refactor this.
				if (input.Count == 0) return null;
				KeyValuePair<string, LfStringField> kv = input.First();
				wsStr = kv.Key;
				value = kv.Value.Value;
				wsId = wsm.GetWsFromStr(wsStr);
				Console.WriteLine("Returning TsString from {0} for writing system {1}", value, wsStr);
				return TsStringUtils.MakeTss(value, wsId);
			}
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

		private ILexEtymology CreateOwnedEtymology(ILexEntry owner)
		{
			ILexEtymologyFactory etymologyFactory = servLoc.GetInstance<ILexEtymologyFactory>();
			ILexEtymology result = etymologyFactory.Create(); // Have to use a different approach for OA fields: factory doesn't have Create(guid, owner)
			owner.EtymologyOA = result;
			return result;
		}

		private IMoForm CreateOwnedLexemeForm(ILexEntry owner, string morphologyType)
		{
			// morphologyType is a string because that's how it's (currently, as of Nov 2015) stored in LF's Mongo database.
//			if (morphologyType == null) // Handled below
//				morphologyType = "root";
			IMoForm result;
			var stemFactory = servLoc.GetInstance<IMoStemAllomorphFactory>();
			var affixFactory = servLoc.GetInstance<IMoAffixAllomorphFactory>();
			// TODO: This list of hardcoded strings might belong in an enum, rather than here
			switch (morphologyType)
			{
			case "bound root":
			case "bound stem":
			case "root":
			case "stem":
			case "particle":
			case "clitic":
			case "proclitic":
			case "enclitic":
			case "phrase":
			case "discontiguous phrase":
			case null:
				result = stemFactory.Create();
				break;

			case "circumfix":
			case "prefix":
			case "suffix":
			case "infix":
			case "prefixing interfix":
			case "infixing interfix":
			case "suffixing interfix":
			case "simulfix":
			case "suprafix":
				result = affixFactory.Create();
				break;

			default:
				Console.WriteLine("WARNING: Unrecognized morphology type \"{0}\" in word {1}", morphologyType, owner.Guid);
				result = stemFactory.Create();
				break;
			}
			owner.LexemeFormOA = result;
			return result;
		}

		private ILexEntry GetOrCreateEntryByGuid(Guid guid)
		{
			ILexEntry result;
			if (!entryRepo.TryGetObject(guid, out result))
				result = entryFactory.Create();
			return result;
		}

		private ILexExampleSentence GetOrCreateExampleByGuid(Guid guid, ILexSense owner)
		{
			ILexExampleSentence result;
			if (!exampleRepo.TryGetObject(guid, out result))
				result = exampleFactory.Create(guid, owner);
			return result;
		}

		private ICmPicture GetOrCreatePictureByGuid(Guid guid)
		{
			ICmPicture result;
			if (!pictureRepo.TryGetObject(guid, out result))
				result = pictureFactory.Create();
			return result;
		}

		private ILexPronunciation GetOrCreatePronunciationByGuid(Guid guid, ILexEntry owner)
		{
			ILexPronunciation result;
			if (!pronunciationRepo.TryGetObject(guid, out result))
			{
				result = pronunciationFactory.Create();
				owner.PronunciationsOS.Add(result);
			}
			return result;
		}

		private ILexSense GetOrCreateSenseByGuid(Guid guid, ILexEntry owner)
		{
			ILexSense result;
			if (!senseRepo.TryGetObject(guid, out result))
				result = senseFactory.Create(guid, owner);
			return result;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

