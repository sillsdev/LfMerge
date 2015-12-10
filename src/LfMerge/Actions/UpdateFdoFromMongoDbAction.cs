// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using Autofac;
using MongoDB.Bson;
using MongoDB.Driver;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
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
		private IMongoConnection connection;
		private MongoProjectRecordFactory projectRecordFactory;


		private ILexEntryRepository entryRepo;
		private ILexExampleSentenceRepository exampleRepo;
		private ICmPictureRepository pictureRepo;
		private ILexPronunciationRepository pronunciationRepo;
		private ILexSenseRepository senseRepo;
		private IPartOfSpeechRepository posRepo;
		private ILexEntryFactory entryFactory;
		private ILexExampleSentenceFactory exampleFactory;
		private ICmPictureFactory pictureFactory;
		private ILexPronunciationFactory pronunciationFactory;
		private ILexSenseFactory senseFactory;

		private CustomFieldConverter customFieldConverter;

		public UpdateFdoFromMongoDbAction(IMongoConnection conn, MongoProjectRecordFactory factory)
		{
			connection = conn;
			projectRecordFactory = factory;
		}

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
			customFieldConverter = new CustomFieldConverter(cache);

			// For efficiency's sake, cache the five repositories and five factories we'll need all the time,
			entryRepo = servLoc.GetInstance<ILexEntryRepository>();
			exampleRepo = servLoc.GetInstance<ILexExampleSentenceRepository>();
			pictureRepo = servLoc.GetInstance<ICmPictureRepository>();
			pronunciationRepo = servLoc.GetInstance<ILexPronunciationRepository>();
			posRepo = servLoc.GetInstance<IPartOfSpeechRepository>();
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
				// TODO: Use cache.ActionHandlerAccessor.Commit() to actually save the file that we've just modified.
			});
		}

		private IEnumerable<LfLexEntry> GetLexiconForTesting(ILfProject project, ILfProjectConfig config)
		{
			var db = connection.GetProjectDatabase(project);
			var collection = db.GetCollection<LfLexEntry>("lexicon");
			IAsyncCursor<LfLexEntry> result2 = collection.Find<LfLexEntry>(_ => true).ToCursorAsync().Result;
			return result2.AsEnumerable();
		}

		private ILfProjectConfig GetConfigForTesting(ILfProject project)
		{
			MongoProjectRecord projectRecord = projectRecordFactory.Create(project);
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
			Guid guid = lfEntry.Guid ?? Guid.Empty;
			ILexEntry fdoEntry = GetOrCreateEntryByGuid(guid);
			if (lfEntry.IsDeleted)
			{
				if (fdoEntry.CanDelete)
					fdoEntry.Delete();
				else
					// TODO: Log this properly
					Console.WriteLine("Problem: need to delete FDO entry {0}, but its CanDelete flag is false.", fdoEntry.Guid);
				return null; // Don't set fields on a deleted entry
			}
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
			// TODO: Do something like the following line (can't do exactly that because PrimaryMorphType is read-only)
			// fdoEntry.PrimaryMorphType = new PossibilityListConverter(fdoEntry.PrimaryMorphType.OwningList).GetByName(lfEntry.MorphologyType) as IMoMorphType;


			/* TODO: Process the following fields too

					fdoEntry.PronunciationsOS.First();
					lfEntry.Environments;
			*/
			/* LfLexEntry fields not mapped:
			lfEntry.CustomFields // TODO: Handle later
			lfEntry.Environments // Don't know how to handle this one. TODO: Research it.
			lfEntry.LiftId // TODO: Figure out how to handle this one. In fdoEntry, it's a constructed value.
			lfEntry.MercurialSha; // Skip: We don't update this until we've committed to the Mercurial repo
			lfEntry.MorphologyType; // TODO: Put this in fdoEntry.PrimaryMorphType

			*/

			if (lfEntry.Senses != null) {
				foreach(LfSense lfSense in lfEntry.Senses)
				{
					#pragma warning disable 0219 // "Variable is assigned but its value is never used"
					ILexSense fdoSense = LfSenseToFdoSense(lfSense, fdoEntry);
					#pragma warning restore 0219
				}
			}

			customFieldConverter.SetCustomFieldsForThisCmObject(fdoEntry, "entry", lfEntry.CustomFields, lfEntry.CustomFieldGuids);

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
			Guid guid = lfExample.Guid;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence fdoExample = GetOrCreateExampleByGuid(guid, owner);
			// Ignoring lfExample.AuthorInfo.CreatedDate;
			// Ignoring lfExample.AuthorInfo.ModifiedDate;
			// Ignoring lfExample.ExampleId; // TODO: is this different from a LIFT ID?
			SetMultiStringFrom(fdoExample.Example, lfExample.Sentence);
			// fdoExample.PublishIn = lfExample.ExamplePublishIn; // TODO: More complex than that.
			fdoExample.Reference = BestStringFromMultiText(lfExample.Reference);
			// fdoExample.TranslationsOC = ToOwningCollection(lfExample.Translation); // TODO: Implement ToOwningCollection<ICmTranslation>(LfMultiText multi)
			/*
			ICmTranslation t;
			t.AvailableWritingSystems;
			t.Status;
			t.Translation;
			t.TypeRA; // Free, literal, etc.
			*/

			customFieldConverter.SetCustomFieldsForThisCmObject(fdoExample, "examples", lfExample.CustomFields, lfExample.CustomFieldGuids);

			return fdoExample;
		}

		private ICmPicture LfPictureToFdoPicture(LfPicture lfPicture, ILexSense owner)
		{
			Guid guid = lfPicture.Guid;
			ICmPicture fdoPicture = GetOrCreatePictureByGuid(guid, owner);
			SetMultiStringFrom(fdoPicture.Caption, lfPicture.Caption);
			// ICmFile f = fdoPicture.PictureFileRA; // TODO: Use a factory, and set from lfPicture.FileName
			// TODO: Any other instance fields?
			return fdoPicture;
		}

		private ILexSense LfSenseToFdoSense(LfSense lfSense, ILexEntry owner)
		{
			Guid guid = lfSense.Guid;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense fdoSense = GetOrCreateSenseByGuid(guid, owner);
			// TODO: Set instance fields

			// var converter = new PossibilityListConverter(cache.LanguageProject.LocationsOA);
			// fdoPronunciation.LocationRA = (ICmLocation)converter.GetByName(lfEntry.Location.Value);
			// TODO: Check if the compiler is happy with the below (creating an object and throwing it away after calling one method)
//			new PossibilityListConverter(cache.LanguageProject.SemanticDomainListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.DomainTypesRC, lfSense.AcademicDomains);
//			new PossibilityListConverter(cache.LanguageProject.AnthroListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.AnthroCodesRC, lfSense.AnthropologyCategories);
			SetMultiStringFrom(fdoSense.AnthroNote, lfSense.AnthropologyNote);
			// lfSense.AuthorInfo; // TODO: Figure out if this should be copied too
			// lfSense.CustomFields; // TODO: Handle these last
			SetMultiStringFrom(fdoSense.Definition, lfSense.Definition);
			SetMultiStringFrom(fdoSense.DiscourseNote, lfSense.DiscourseNote);
			SetMultiStringFrom(fdoSense.EncyclopedicInfo, lfSense.EncyclopedicNote);
			foreach (LfExample lfExample in lfSense.Examples)
			{
				#pragma warning disable 0219 // "Variable is assigned but its value is never used"
				ILexExampleSentence fdoExample = LfExampleToFdoExample(lfExample, fdoSense);
				#pragma warning restore 0219
				// TODO: Implement LfExampleToFdoExample function, then either add any necessary outside-the-function code here or delete this comment
			};
			SetMultiStringFrom(fdoSense.GeneralNote, lfSense.GeneralNote);
			SetMultiStringFrom(fdoSense.Gloss, lfSense.Gloss);
			SetMultiStringFrom(fdoSense.GrammarNote, lfSense.GrammarNote);
			// fdoSense.LIFTid = lfSense.LiftId; // Read-only property in FDO Sense, doesn't make sense to set it. TODO: Is that correct?
			if (lfSense.PartOfSpeech != null)
			{
				IPartOfSpeech pos = PartOfSpeechConverter.FromName(lfSense.PartOfSpeech.ToString(), posRepo);
				if (pos != null) // TODO: If it's null, PartOfSpeechConverter.FromName will eventually create it. Once that happens, this check can be removed.
				{
					PartOfSpeechConverter.SetPartOfSpeech(fdoSense.MorphoSyntaxAnalysisRA, pos);
					Console.WriteLine("Part of speech of {0} has been set to {1}", fdoSense.MorphoSyntaxAnalysisRA.GetGlossOfFirstSense(), pos.AbbrAndName);
				}
			}
			// fdoSense.MorphoSyntaxAnalysisRA.MLPartOfSpeech = lfSense.PartOfSpeech; // TODO: FAR more complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.PhonologyNote, lfSense.PhonologyNote);
			foreach (LfPicture lfPicture in lfSense.Pictures)
			{
				#pragma warning disable 0219 // "Variable is assigned but its value is never used"
				ICmPicture fdoPicture = LfPictureToFdoPicture(lfPicture, fdoSense);
				#pragma warning restore 0219
				// TODO: Implement LfPictureToFdoPicture function, then either add any necessary outside-the-function code here or delete this comment
			}
			// fdoSense.ReversalEntriesRC = lfSense.ReversalEntries; // TODO: More complex than that. Handle it correctly. Maybe.
			fdoSense.ScientificName = BestStringFromMultiText(lfSense.ScientificName);
//			new PossibilityListConverter(cache.LanguageProject.SemanticDomainListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.SemanticDomainsRC, lfSense.SemanticDomain);
			SetMultiStringFrom(fdoSense.SemanticsNote, lfSense.SemanticsNote);
			SetMultiStringFrom(fdoSense.Bibliography, lfSense.SenseBibliography);

			// lfSense.SenseId; // TODO: What do I do with this one?
			fdoSense.ImportResidue = BestStringFromMultiText(lfSense.SenseImportResidue);
			// fdoSense.PublishIn = lfSense.SensePublishIn; // TODO: More complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.Restrictions, lfSense.SenseRestrictions);
			fdoSense.SenseTypeRA = new PossibilityListConverter(cache.LanguageProject.LexDbOA.SenseTypesOA).GetByName(lfSense.SenseType);
			SetMultiStringFrom(fdoSense.SocioLinguisticsNote, lfSense.SociolinguisticsNote);
			fdoSense.Source = BestStringFromMultiText(lfSense.Source);
			// fdoSense.StatusRA = new PossibilityListConverter(cache.LanguageProject.StatusOA).GetByName(lfSense.Status); // TODO: Nope, more complex.
			// fdoSense.UsageTypesRC = lfSense.Usages; // TODO: More complex than that. Handle it correctly.

			customFieldConverter.SetCustomFieldsForThisCmObject(fdoSense, "senses", lfSense.CustomFields, lfSense.CustomFieldGuids);

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

		private ICmPicture GetOrCreatePictureByGuid(Guid guid, ILexSense owner)
		{
			ICmPicture result;
			if (!pictureRepo.TryGetObject(guid, out result))
			{
				result = pictureFactory.Create();
				owner.PicturesOS.Add(result);
			}
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

