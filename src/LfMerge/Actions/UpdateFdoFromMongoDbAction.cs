// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using MongoDB.Driver;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Actions
{
	public class UpdateFdoFromMongoDbAction: Action
	{
		private FdoCache _cache;
		private IFdoServiceLocator _servLoc;
		private IMongoConnection _connection;
		private MongoProjectRecordFactory _projectRecordFactory;
		private ILfProject _lfProject;
		private MongoProjectRecord _projectRecord;

		private ILfProjectConfig _lfProjectConfig;

		private ILexEntryRepository _entryRepo;
		private ILexExampleSentenceRepository _exampleRepo;
		private ICmPictureRepository _pictureRepo;
		private ILexPronunciationRepository _pronunciationRepo;
		private ILexSenseRepository _senseRepo;
		private ICmTranslationRepository _translationRepo;
		private ILexEntryFactory _entryFactory;
		private ILexExampleSentenceFactory _exampleFactory;
		private ICmPictureFactory _pictureFactory;
		private ILexPronunciationFactory _pronunciationFactory;
		private ILexSenseFactory _senseFactory;
		private ICmTranslationFactory _translationFactory;

		private ICmPossibility _freeTranslationType; // Used in LfExampleToFdoExample(), but cached here

		private CustomFieldConverter _customFieldConverter;

		public UpdateFdoFromMongoDbAction(LfMergeSettingsIni settings, IMongoConnection conn, MongoProjectRecordFactory factory) : base(settings)
		{
			_connection = conn;
			_projectRecordFactory = factory;
		}

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.QUEUED; }
		}

		protected override void DoRun(ILfProject project)
		{
			_lfProject = project;
			_projectRecord = _projectRecordFactory.Create(_lfProject);
			if (_projectRecord == null)
			{
				Console.WriteLine("No project named {0}", _lfProject.LfProjectCode);
				Console.WriteLine("If we are unit testing, this may not be an error");
				return;
			}
			_lfProjectConfig = _projectRecord.Config;
			if (_lfProjectConfig == null)
				return;

			if (project.FieldWorksProject == null)
			{
				Console.WriteLine("Failed to find the corresponding FieldWorks project!");
				return;
			}
			Console.WriteLine("Project {0} disposed", project.FieldWorksProject.IsDisposed ? "is" : "is not");
			_cache = project.FieldWorksProject.Cache;
			if (_cache == null)
			{
				Console.WriteLine("Failed to find the FDO cache!");
				FwProject fwProject = project.FieldWorksProject;
				return;
			}

			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Console.WriteLine("Failed to find the service locator; giving up.");
				return;
			}
			_customFieldConverter = new CustomFieldConverter(_cache);

			// For efficiency's sake, cache the five repositories and five factories we'll need all the time,
			_entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			_exampleRepo = _servLoc.GetInstance<ILexExampleSentenceRepository>();
			_pictureRepo = _servLoc.GetInstance<ICmPictureRepository>();
			_pronunciationRepo = _servLoc.GetInstance<ILexPronunciationRepository>();
			_senseRepo = _servLoc.GetInstance<ILexSenseRepository>();
			_translationRepo = _servLoc.GetInstance<ICmTranslationRepository>();
			_entryFactory = _servLoc.GetInstance<ILexEntryFactory>();
			_exampleFactory = _servLoc.GetInstance<ILexExampleSentenceFactory>();
			_pictureFactory = _servLoc.GetInstance<ICmPictureFactory>();
			_pronunciationFactory = _servLoc.GetInstance<ILexPronunciationFactory>();
			_senseFactory = _servLoc.GetInstance<ILexSenseFactory>();
			_translationFactory = _servLoc.GetInstance<ICmTranslationFactory>();

			if (_cache.LanguageProject != null && _cache.LanguageProject.TranslationTagsOA != null)
			{
				// TODO: Consider using LangProjectTags.kguidTranFreeTranslation instead
				_freeTranslationType = new PossibilityListConverter(_cache.LanguageProject.TranslationTagsOA).GetByName("Free translation");
				if (_freeTranslationType == null)
					_freeTranslationType = _cache.LanguageProject.TranslationTagsOA.PossibilitiesOS.FirstOrDefault();
			}

			IEnumerable<LfLexEntry> lexicon = GetLexiconForTesting(project, _lfProjectConfig);
			NonUndoableUnitOfWorkHelper.Do(_cache.ActionHandlerAccessor, () =>
			{
				foreach (LfLexEntry lfEntry in lexicon)
					LfLexEntryToFdoLexEntry(lfEntry);
				// TODO: Use _cache.ActionHandlerAccessor.Commit() to actually save the file that we've just modified.
			});
		}

		private IEnumerable<LfLexEntry> GetLexiconForTesting(ILfProject project, ILfProjectConfig config)
		{
			IMongoDatabase db = _connection.GetProjectDatabase(project);
			IMongoCollection<LfLexEntry> collection = db.GetCollection<LfLexEntry>("lexicon");
			IAsyncCursor<LfLexEntry> result = collection.Find<LfLexEntry>(_ => true).ToCursor();
			return result.AsEnumerable();
		}

		private ILfProjectConfig GetConfigForTesting(ILfProject project)
		{
			ILfProjectConfig config = _projectRecord.Config;
			Console.WriteLine(config.GetType()); // Should be LfMerge.LanguageForge.Config.LfProjectConfig
			Console.WriteLine(config.Entry.Type);
			Console.WriteLine(String.Join(", ", config.Entry.FieldOrder));
			Console.WriteLine(config.Entry.Fields["lexeme"].Type);
			Console.WriteLine(config.Entry.Fields["lexeme"].GetType());
			return config;
		}

		private Guid GuidFromLiftId(string liftId)
		{
			Guid result;
			if (String.IsNullOrEmpty(liftId))
				return default(Guid);
			if (Guid.TryParse(liftId, out result))
				return result;
			int pos = liftId.LastIndexOf('_');
			if (Guid.TryParse(liftId.Substring(pos+1), out result))
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
				source.WriteToFdoMultiString(dest, _servLoc.WritingSystemManager);
		}

		private void LfLexEntryToFdoLexEntry(LfLexEntry lfEntry)
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
				return; // Don't set fields on a deleted entry
			}
			string entryNameForDebugging = String.Join(", ", lfEntry.Lexeme.Values.Select(x => x.Value ?? ""));
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
					lfEntry.Environments;
			*/

			/* LfLexEntry fields not mapped:
			lfEntry.Environments // Don't know how to handle this one. TODO: Research it.
			lfEntry.LiftId // TODO: Figure out how to handle this one. In fdoEntry, it's a constructed value.
			lfEntry.MercurialSha; // Skip: We don't update this until we've committed to the Mercurial repo

			*/

			if (lfEntry.Senses != null) {
				foreach(LfSense lfSense in lfEntry.Senses)
					LfSenseToFdoSense(lfSense, fdoEntry);
			}

			_customFieldConverter.SetCustomFieldsForThisCmObject(fdoEntry, "entry", lfEntry.CustomFields, lfEntry.CustomFieldGuids);
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
				var converter = new PossibilityListConverter(_cache.LanguageProject.LocationsOA);
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
				// Console.WriteLine("non-null input, but no contents in it!"); // TODO: Turn this into a log message
				return null;
			}
			WritingSystemManager wsm = _cache.ServiceLocator.WritingSystemManager;
			if (wsm == null) return null;

			List<int> wsIdsToSearch;
			IWritingSystemContainer wsc = _cache.ServiceLocator.WritingSystems;
			if (wsc != null)
			{
				IList<CoreWritingSystemDefinition> analysisWritingSystems = wsc.CurrentAnalysisWritingSystems;
				wsIdsToSearch = analysisWritingSystems.Select(ws => ws.Handle).ToList();
			}
			else
			{
				wsIdsToSearch = new List<int> { _cache.DefaultAnalWs };
			}
			int fallbackWsId = _cache.DefaultUserWs;
			wsIdsToSearch.Add(fallbackWsId);

			foreach (int wsId in wsIdsToSearch)
			{
				string wsStr = wsm.GetStrFromWs(wsId);
				LfStringField field;
				if (input.TryGetValue(wsStr, out field) && !String.IsNullOrEmpty(field.Value))
				{
					Console.WriteLine("Returning TsString from {0} for writing system {1}", field.Value, wsStr);
					return TsStringUtils.MakeTss(field.Value, wsId);
				}
			}

			// Last-ditch option: just grab the first non-empty string we can find
			KeyValuePair<int, string> kv = input.WsIdAndFirstNonEmptyString(_cache);
			if (kv.Value == null) return null;
			Console.WriteLine("Returning TsString from {0} for writing system {1}", kv.Value, wsm.GetStrFromWs(kv.Key));
			return TsStringUtils.MakeTss(kv.Value, kv.Key);
		}

		private ICmTranslation FindOrCreateTranslationByGuid(Guid guid, ILexExampleSentence owner, ICmPossibility typeOfNewTranslation)
		{
			// If it's already in the owning list, use that object
			ICmTranslation result = owner.TranslationsOC.FirstOrDefault(t => t.Guid == guid);
			if (result != null)
				return result;
			// Does a translation with that GUID already exist elsewhere?
			if (_translationRepo.TryGetObject(guid, out result))
			{
				// Move it "here". No formerOwner.Remove() needed since TranslationsOC.Add() takes care of that.
				owner.TranslationsOC.Add(result);
				return result;
			}
			// Not found anywhere: make a new one.
			return _translationFactory.Create(owner, typeOfNewTranslation);
		}

		private void LfExampleToFdoExample(LfExample lfExample, ILexSense owner)
		{
			Guid guid = lfExample.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence fdoExample = GetOrCreateExampleByGuid(guid, owner);
			// Ignoring lfExample.AuthorInfo.CreatedDate;
			// Ignoring lfExample.AuthorInfo.ModifiedDate;
			// Ignoring lfExample.ExampleId; // TODO: is this different from a LIFT ID?
			SetMultiStringFrom(fdoExample.Example, lfExample.Sentence);
			// fdoExample.PublishIn = lfExample.ExamplePublishIn; // TODO: More complex than that.
			fdoExample.Reference = BestStringFromMultiText(lfExample.Reference);
			ICmTranslation t = FindOrCreateTranslationByGuid(lfExample.TranslationGuid, fdoExample, _freeTranslationType);
			SetMultiStringFrom(t.Translation, lfExample.Translation);
			// TODO: Set t.AvailableWritingSystems appropriately
			// Ignoring t.Status since LF won't touch it

			_customFieldConverter.SetCustomFieldsForThisCmObject(fdoExample, "examples", lfExample.CustomFields, lfExample.CustomFieldGuids);
		}

		private void LfPictureToFdoPicture(LfPicture lfPicture, ILexSense owner)
		{
			Guid guid = lfPicture.Guid ?? Guid.Empty;
			KeyValuePair<int, string> kv = lfPicture.Caption.WsIdAndFirstNonEmptyString(_cache);
			int captionWs = kv.Key;
			string caption = kv.Value;
			/* ICmPicture fdoPicture = */ GetOrCreatePictureByGuid(guid, owner, lfPicture.FileName, caption, captionWs);
			// Ignoring fdoPicture.Description and other fdoPicture fields since LF won't touch them
		}

		private void LfSenseToFdoSense(LfSense lfSense, ILexEntry owner)
		{
			Guid guid = lfSense.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense fdoSense = GetOrCreateSenseByGuid(guid, owner);
			// TODO: Set instance fields

			// var converter = new PossibilityListConverter(_cache.LanguageProject.LocationsOA);
			// fdoPronunciation.LocationRA = (ICmLocation)converter.GetByName(lfEntry.Location.Value);
			// TODO: Check if the compiler is happy with the below (creating an object and throwing it away after calling one method)
//			new PossibilityListConverter(_cache.LanguageProject.SemanticDomainListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.DomainTypesRC, lfSense.AcademicDomains);
//			new PossibilityListConverter(_cache.LanguageProject.AnthroListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.AnthroCodesRC, lfSense.AnthropologyCategories);
			SetMultiStringFrom(fdoSense.AnthroNote, lfSense.AnthropologyNote);
			// lfSense.AuthorInfo; // TODO: Figure out if this should be copied too
			SetMultiStringFrom(fdoSense.Definition, lfSense.Definition);
			SetMultiStringFrom(fdoSense.DiscourseNote, lfSense.DiscourseNote);
			SetMultiStringFrom(fdoSense.EncyclopedicInfo, lfSense.EncyclopedicNote);
			foreach (LfExample lfExample in lfSense.Examples)
				LfExampleToFdoExample(lfExample, fdoSense);
			SetMultiStringFrom(fdoSense.GeneralNote, lfSense.GeneralNote);
			SetMultiStringFrom(fdoSense.Gloss, lfSense.Gloss);
			SetMultiStringFrom(fdoSense.GrammarNote, lfSense.GrammarNote);
			// fdoSense.LIFTid = lfSense.LiftId; // Read-only property in FDO Sense, doesn't make sense to set it. TODO: Is that correct?
			if (lfSense.PartOfSpeech != null)
			{
				var posConverter = new PartOfSpeechConverter(_cache);
				//string userWs = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultUserWs);
				string userWs = _projectRecord.InterfaceLanguageCode;
				if (String.IsNullOrEmpty(userWs))
					userWs = "en";
				IPartOfSpeech pos = posConverter.FromName(lfSense.PartOfSpeech.ToString(), userWs);
				if (pos != null) // TODO: If it's null, PartOfSpeechConverter.FromName will eventually create it. Once that happens, this check can be removed.
				{
					PartOfSpeechConverter.SetPartOfSpeech(fdoSense.MorphoSyntaxAnalysisRA, pos);
					Console.WriteLine("Part of speech of {0} has been set to {1}", fdoSense.MorphoSyntaxAnalysisRA.GetGlossOfFirstSense(), pos);
				}
			}
			// fdoSense.MorphoSyntaxAnalysisRA.MLPartOfSpeech = lfSense.PartOfSpeech; // TODO: FAR more complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.PhonologyNote, lfSense.PhonologyNote);
			foreach (LfPicture lfPicture in lfSense.Pictures)
				LfPictureToFdoPicture(lfPicture, fdoSense);
			// fdoSense.ReversalEntriesRC = lfSense.ReversalEntries; // TODO: More complex than that. Handle it correctly. Maybe.
			fdoSense.ScientificName = BestStringFromMultiText(lfSense.ScientificName);
//			new PossibilityListConverter(_cache.LanguageProject.SemanticDomainListOA)
//				.UpdatePossibilitiesFromStringArray(fdoSense.SemanticDomainsRC, lfSense.SemanticDomain);
			SetMultiStringFrom(fdoSense.SemanticsNote, lfSense.SemanticsNote);
			SetMultiStringFrom(fdoSense.Bibliography, lfSense.SenseBibliography);

			// lfSense.SenseId; // TODO: What do I do with this one?
			fdoSense.ImportResidue = BestStringFromMultiText(lfSense.SenseImportResidue);
			// fdoSense.PublishIn = lfSense.SensePublishIn; // TODO: More complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.Restrictions, lfSense.SenseRestrictions);
			fdoSense.SenseTypeRA = new PossibilityListConverter(_cache.LanguageProject.LexDbOA.SenseTypesOA).GetByName(lfSense.SenseType);
			SetMultiStringFrom(fdoSense.SocioLinguisticsNote, lfSense.SociolinguisticsNote);
			fdoSense.Source = BestStringFromMultiText(lfSense.Source);
			// fdoSense.StatusRA = new PossibilityListConverter(_cache.LanguageProject.StatusOA).GetByName(lfSense.Status); // TODO: Nope, more complex.
			// fdoSense.UsageTypesRC = lfSense.Usages; // TODO: More complex than that. Handle it correctly.

			_customFieldConverter.SetCustomFieldsForThisCmObject(fdoSense, "senses", lfSense.CustomFields, lfSense.CustomFieldGuids);
		}

		private ILexEtymology CreateOwnedEtymology(ILexEntry owner)
		{
			ILexEtymologyFactory etymologyFactory = _servLoc.GetInstance<ILexEtymologyFactory>();
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
			var stemFactory = _servLoc.GetInstance<IMoStemAllomorphFactory>();
			var affixFactory = _servLoc.GetInstance<IMoAffixAllomorphFactory>();
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
			if (!_entryRepo.TryGetObject(guid, out result))
				result = _entryFactory.Create();
			return result;
		}

		private ILexExampleSentence GetOrCreateExampleByGuid(Guid guid, ILexSense owner)
		{
			ILexExampleSentence result;
			if (!_exampleRepo.TryGetObject(guid, out result))
				result = _exampleFactory.Create(guid, owner);
			return result;
		}

		private ICmPicture GetOrCreatePictureByGuid(Guid guid, ILexSense owner, string fileName, string caption, int captionWs)
		{
			ICmPicture result;
			if (!_pictureRepo.TryGetObject(guid, out result))
			{
				if (caption == null)
					caption = "";
				ITsString captionTss = TsStringUtils.MakeTss(caption, captionWs);
				result = _pictureFactory.Create(fileName, captionTss, CmFolderTags.LocalPictures);
				owner.PicturesOS.Add(result);
			}
			return result;
		}

		private ILexPronunciation GetOrCreatePronunciationByGuid(Guid guid, ILexEntry owner)
		{
			ILexPronunciation result;
			if (!_pronunciationRepo.TryGetObject(guid, out result))
			{
				result = _pronunciationFactory.Create();
				owner.PronunciationsOS.Add(result);
			}
			return result;
		}

		private ILexSense GetOrCreateSenseByGuid(Guid guid, ILexEntry owner)
		{
			ILexSense result;
			if (!_senseRepo.TryGetObject(guid, out result))
				result = _senseFactory.Create(guid, owner);
			return result;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

