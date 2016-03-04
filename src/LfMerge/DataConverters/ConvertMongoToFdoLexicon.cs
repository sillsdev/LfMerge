// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using MongoDB.Driver;
using SIL.CoreImpl; // For TsStringUtils
using SIL.FieldWorks.Common.COMInterfaces; // For ITsString
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LfMerge.DataConverters
{
	public class ConvertMongoToFdoLexicon
	{
		public LfMergeSettingsIni Settings { get; set; }
		public ILfProject LfProject { get; set; }
		public FwProject FwProject { get; set; }
		public FdoCache Cache { get; set; }
		public ILogger Logger { get; set; }
		public IMongoConnection Connection { get; set; }
		public MongoProjectRecord ProjectRecord { get; set; }

		public IEnumerable<CoreWritingSystemDefinition> AnalysisWritingSystems;
		public IEnumerable<CoreWritingSystemDefinition> VernacularWritingSystems;

		private int _wsEn;
		private ConvertMongoToFdoPartsOfSpeech _posConverter;
		private CustomFieldConverter _customFieldConverter;
		private LfOptionList _lfGrammar;
		private Dictionary<string, LfOptionListItem> _lfGrammarByKey;

		private ICmPossibility _freeTranslationType; // Used in LfExampleToFdoExample(), but cached here

		public ConvertMongoToFdoLexicon(LfMergeSettingsIni settings, ILfProject lfproject, ILogger logger, IMongoConnection connection, MongoProjectRecord projectRecord)
		{
			Settings = settings;
			LfProject = lfproject;
			Logger = logger;
			Connection = connection;
			ProjectRecord = projectRecord;

			FwProject = LfProject.FieldWorksProject;
			Cache = FwProject.Cache;
			// These writing system search orders will be used in BestStringAndWsFromMultiText and related functions
			AnalysisWritingSystems = Cache.LanguageProject.CurrentAnalysisWritingSystems;
			VernacularWritingSystems = Cache.LanguageProject.CurrentVernacularWritingSystems;

			_customFieldConverter = new CustomFieldConverter(Cache);
			_posConverter = new ConvertMongoToFdoPartsOfSpeech(Cache);

			_lfGrammar = Connection.GetLfOptionListByCode(LfProject, MagicStrings.LfOptionListCodeForGrammaticalInfo);
			if (_lfGrammar == null)
				_lfGrammarByKey = new Dictionary<string, LfOptionListItem>();
			else
				_lfGrammarByKey = _lfGrammar.Items.ToDictionary(item => item.Key, item => item);

			if (Cache.LanguageProject != null && Cache.LanguageProject.TranslationTagsOA != null)
			{
				_freeTranslationType = Cache.ServiceLocator.ObjectRepository.GetObject(LangProjectTags.kguidTranFreeTranslation) as ICmPossibility;
				if (_freeTranslationType == null) // Shouldn't happen, but let's have a fallback possibility
					_freeTranslationType = Cache.LanguageProject.TranslationTagsOA.PossibilitiesOS.FirstOrDefault();
			}

			_wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");
		}

		public void RunConversion()
		{
			// Update writing systems from project config input systems.  Won't commit till the end
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", Cache.ActionHandlerAccessor, () =>
				{
					LfWsToFdoWs(ProjectRecord.InputSystems);
				});

			// Set English ws handle again in case it changed
			_wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");

			_customFieldConverter = new CustomFieldConverter(Cache);
			_posConverter = new ConvertMongoToFdoPartsOfSpeech(Cache);

			IEnumerable<LfLexEntry> lexicon = GetLexicon(LfProject);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", Cache.ActionHandlerAccessor, () =>
				{
					if (_lfGrammar != null)
						UpdateFdoGrammarFromLfGrammar(_lfGrammar);
					foreach (LfLexEntry lfEntry in lexicon)
						LfLexEntryToFdoLexEntry(lfEntry);
				});
			if (Settings.CommitWhenDone)
				Cache.ActionHandlerAccessor.Commit();
			Logger.Debug("FdoFromMongoDb: done");
		}

		// Shorthand for getting an instance from the cache's service locator
		public T GetInstance<T>()
		{
			return Cache.ServiceLocator.GetInstance<T>();
		}

		// Shorthand for getting an instance (keyed by a string key) from the cache's service locator
		public T GetInstance<T>(string key)
		{
			return Cache.ServiceLocator.GetInstance<T>(key);
		}

		public IEnumerable<LfLexEntry> GetLexicon(ILfProject project)
		{
			return Connection.GetRecords<LfLexEntry>(project, MagicStrings.LfCollectionNameForLexicon);
		}

		/// <summary>
		/// Converts the list of LF input systems and adds them to FDO writing systems
		/// </summary>
		/// <param name="lfWsList">List of LF input systems.</param>
		public void LfWsToFdoWs(Dictionary<string, LfInputSystemRecord> lfWsList)
		{
			WritingSystemManager wsm = Cache.ServiceLocator.WritingSystemManager;
			if (wsm == null)
			{
				Logger.Error("Failed to find the writing system manager");
				return;
			}

			string vernacularLanguageCode = ProjectRecord.LanguageCode;
			// TODO: Split the inside of this foreach() out into its own function
			foreach (var lfWs in lfWsList.Values)
			{
				CoreWritingSystemDefinition ws;

				// It would be nice to call this to add to both analysis and vernacular WS.
				// But we need the flexibility of bringing in LF WS properties.
				/*
				if (!WritingSystemServices.FindOrCreateSomeWritingSystem(
					Cache, null, lfWs.Tag, true, true, out ws))
				{
					// Could neither find NOR create a writing system, probably because the tag was malformed? Log it and move on.
					Logger.Warning("Failed to find or create an FDO writing system corresponding to tag {0}. Is it malformed?", lfWs.Tag);
					continue;
				}
				*/

				if (wsm.TryGet(lfWs.Tag, out ws))
				{
					ws.Abbreviation = lfWs.Abbreviation;
					ws.RightToLeftScript = lfWs.IsRightToLeft;
					wsm.Replace(ws);
				}
				else
				{
					ws = wsm.Create(lfWs.Tag);
					ws.Abbreviation = lfWs.Abbreviation;
					ws.RightToLeftScript = lfWs.IsRightToLeft;
					wsm.Set(ws);

					// LF doesn't distinguish between vernacular/analysis WS, so we'll
					// only assign the project language code to vernacular.
					// All other WS assigned to analysis.
					if (lfWs.Tag.Equals(vernacularLanguageCode))
						Cache.LanguageProject.AddToCurrentVernacularWritingSystems(ws);
					else
						Cache.LanguageProject.AddToCurrentAnalysisWritingSystems(ws);
					
				}
			}
		}

		public Tuple<string, int> BestStringAndWsFromMultiText(LfMultiText input, bool isAnalysisField = true)
		{
			if (input == null) return null;
			if (input.Count == 0)
			{
				Logger.Warning("BestStringAndWsFromMultiText got a non-null multitext, but it was empty. Empty LF MultiText objects should be nulls in Mongo. Unfortunately, at this point in the code it's hard to know which multitext it was.");
				return null;
			}

			IEnumerable<CoreWritingSystemDefinition> wsesToSearch = isAnalysisField ?
				Cache.LanguageProject.AnalysisWritingSystems :
				Cache.LanguageProject.VernacularWritingSystems;
//			List<Tuple<int, string>> wsesToSearch = isAnalysisField ?
//				_analysisWsIdsAndNamesInSearchOrder :
//				_vernacularWsIdsAndNamesInSearchOrder;

			foreach (CoreWritingSystemDefinition ws in wsesToSearch)
			{
				LfStringField field;
				if (input.TryGetValue(ws.Id, out field) && !String.IsNullOrEmpty(field.Value))
				{
					Logger.Info("Returning TsString from {0} for writing system {1}", field.Value, ws.Id);
					return new Tuple<string, int>(field.Value, ws.Handle);
				}
			}

			// Last-ditch option: just grab the first non-empty string we can find
			KeyValuePair<int, string> kv = input.WsIdAndFirstNonEmptyString(Cache);
			if (kv.Value == null) return null;
			Logger.Info("Returning first non-empty TsString from {0} for writing system with ID {1}", kv.Value, kv.Key);
			return new Tuple<string, int>(kv.Value, kv.Key);
		}

		public ITsString BestTsStringFromMultiText(LfMultiText input, bool isAnalysisField = true)
		{
			Tuple<string, int> stringAndWsId = BestStringAndWsFromMultiText(input, isAnalysisField);
			if (stringAndWsId == null)
				return null;
			return TsStringUtils.MakeTss(stringAndWsId.Item1, stringAndWsId.Item2);
		}

		public string BestStringFromMultiText(LfMultiText input, bool isAnalysisField = true)
		{
			Tuple<string, int> stringAndWsId = BestStringAndWsFromMultiText(input, isAnalysisField);
			if (stringAndWsId == null)
				return null;
			return stringAndWsId.Item1;
		}

		public ILexEntry GetOrCreateEntryByGuid(Guid guid)
		{
			ILexEntry result;
			if (!GetInstance<ILexEntryRepository>().TryGetObject(guid, out result))
				result = GetInstance<ILexEntryFactory>().Create(guid, Cache.LanguageProject.LexDbOA);
			return result;
		}

		public ILexExampleSentence GetOrCreateExampleByGuid(Guid guid, ILexSense owner)
		{
			ILexExampleSentence result;
			if (!GetInstance<ILexExampleSentenceRepository>().TryGetObject(guid, out result))
				result = GetInstance<ILexExampleSentenceFactory>().Create(guid, owner);
			return result;
		}

		public ICmPicture GetOrCreatePictureByGuid(Guid guid, ILexSense owner, string pictureName, string caption, int captionWs)
		{
			ICmPicture result;
			if (!GetInstance<ICmPictureRepository>().TryGetObject(guid, out result))
			{
				if (caption == null)
				{
					caption = "";
					captionWs = Cache.DefaultAnalWs;
				}
				ITsString captionTss = TsStringUtils.MakeTss(caption, captionWs);
				// NOTE: The CmPictureFactory class doesn't allow us to specify the GUID of the
				// created Picture object. So we can't rely on GUIDs for round-tripping pictures.
				// TODO: Look into what we *can* rely on for round-tripping pictures.
				result = GetInstance<ICmPictureFactory>().Create(pictureName, captionTss, CmFolderTags.LocalPictures);
				owner.PicturesOS.Add(result);
			}
			return result;
		}

		public ILexPronunciation GetOrCreatePronunciationByGuid(Guid guid, ILexEntry owner)
		{
			ILexPronunciation result;
			if (!GetInstance<ILexPronunciationRepository>().TryGetObject(guid, out result))
			{
				result = GetInstance<ILexPronunciationFactory>().Create();
				owner.PronunciationsOS.Add(result);
			}
			return result;
		}

		public ILexSense GetOrCreateSenseByGuid(Guid guid, ILexEntry owner)
		{
			ILexSense result;
			if (!GetInstance<ILexSenseRepository>().TryGetObject(guid, out result))
				result = GetInstance<ILexSenseFactory>().Create(guid, owner);
			return result;
		}

		public ICmTranslation FindOrCreateTranslationByGuid(Guid guid, ILexExampleSentence owner, ICmPossibility typeOfNewTranslation)
		{
			// If it's already in the owning list, use that object
			ICmTranslation result = owner.TranslationsOC.FirstOrDefault(t => t.Guid == guid);
			if (result != null)
				return result;
			// Does a translation with that GUID already exist elsewhere?
			if (GetInstance<ICmTranslationRepository>().TryGetObject(guid, out result))
			{
				// Move it "here". No formerOwner.Remove() needed since TranslationsOC.Add() takes care of that.
				owner.TranslationsOC.Add(result);
				return result;
			}
			// Not found anywhere: make a new one.
			return GetInstance<ICmTranslationFactory>().Create(owner, typeOfNewTranslation);
		}

		public ILexEtymology CreateOwnedEtymology(ILexEntry owner)
		{
			// Have to use a different approach for OA fields: factory doesn't have Create(guid, owner)
			ILexEtymology result = GetInstance<ILexEtymologyFactory>().Create();
			owner.EtymologyOA = result;
			return result;
		}

		public IMoForm CreateOwnedLexemeForm(ILexEntry owner, string morphologyType)
		{
			// morphologyType is a string because that's how it's (currently, as of Nov 2015) stored in LF's Mongo database.
			IMoForm result;
			var stemFactory = GetInstance<IMoStemAllomorphFactory>();
			var affixFactory = GetInstance<IMoAffixAllomorphFactory>();
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
			case null: // Consider "stem" as the default in case of null
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
				Logger.Warning("Unrecognized morphology type \"{0}\" in word {1}", morphologyType, owner.Guid);
				result = stemFactory.Create();
				break;
			}
			owner.LexemeFormOA = result;
			return result;
		}

		public void LfLexEntryToFdoLexEntry(LfLexEntry lfEntry)
		{
			Guid guid = lfEntry.Guid ?? Guid.Empty;
			ILexEntry fdoEntry = GetOrCreateEntryByGuid(guid);
			if (lfEntry.IsDeleted)
			{
				if (fdoEntry.CanDelete)
					fdoEntry.Delete();
				else
					Logger.Warning("Problem: need to delete FDO entry {0}, but its CanDelete flag is false.", fdoEntry.Guid);
				return; // Don't set fields on a deleted entry
			}
			string entryNameForDebugging = String.Join(", ", lfEntry.Lexeme.Values.Select(x => x.Value ?? ""));
			Logger.Notice("Processing entry {0} ({1}) from LF lexicon", guid, entryNameForDebugging);

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

		public void LfExampleToFdoExample(LfExample lfExample, ILexSense owner)
		{
			Guid guid = lfExample.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence fdoExample = GetOrCreateExampleByGuid(guid, owner);
			// Ignoring lfExample.AuthorInfo.CreatedDate;
			// Ignoring lfExample.AuthorInfo.ModifiedDate;
			// Ignoring lfExample.ExampleId; // TODO: is this different from a LIFT ID?
			SetMultiStringFrom(fdoExample.Example, lfExample.Sentence);
			Logger.Info("FDO Example just got set to {0} for GUID {1} and HVO {2}",
				fdoExample.Example.BestAnalysisVernacularAlternative.Text,
				fdoExample.Guid,
				fdoExample.Hvo
			);
			// fdoExample.PublishIn = lfExample.ExamplePublishIn; // TODO: More complex than that.
			fdoExample.Reference = BestTsStringFromMultiText(lfExample.Reference);
			ICmTranslation t = FindOrCreateTranslationByGuid(lfExample.TranslationGuid, fdoExample, _freeTranslationType);
			SetMultiStringFrom(t.Translation, lfExample.Translation);
			// TODO: Set t.AvailableWritingSystems appropriately
			// Ignoring t.Status since LF won't touch it

			_customFieldConverter.SetCustomFieldsForThisCmObject(fdoExample, "examples", lfExample.CustomFields, lfExample.CustomFieldGuids);
		}

		/// <summary>
		/// Converts LF picture into FDO picture.  Internal FDO pictures will need to have the
		/// directory path "Pictures/" prepended to the filename.  Externally linked picture names won't be modifed.
		/// </summary>
		/// <param name="lfPicture">Lf picture.</param>
		/// <param name="owner">Owning sense.</param>
		public void LfPictureToFdoPicture(LfPicture lfPicture, ILexSense owner)
		{
			Guid guid = lfPicture.Guid ?? Guid.Empty;
			int captionWs = Cache.DefaultAnalWs;
			string caption = "";
			if (lfPicture.Caption != null)
			{
				KeyValuePair<int, string> kv = lfPicture.Caption.WsIdAndFirstNonEmptyString(Cache);
				captionWs = kv.Key;
				caption = kv.Value;
			}

			// FDO expects internal pictures in a certain path.  If an external path already
			// exists, leave it alone.
			string pictureName = lfPicture.FileName;
			Regex regex = new Regex(@"[/\\]");
			const string fdoPicturePath = "Pictures/";
			string picturePath = regex.Match(pictureName).Success ? pictureName : string.Format("{0}{1}", fdoPicturePath, pictureName);

			ICmPicture fdoPicture = GetOrCreatePictureByGuid(guid, owner, picturePath, caption, captionWs);
			// FDO currently only allows one caption to be created with the picture, so set the other captions afterwards
			SetMultiStringFrom(fdoPicture.Caption, lfPicture.Caption);
			// Ignoring fdoPicture.Description and other fdoPicture fields since LF won't touch them
		}

		public void LfSenseToFdoSense(LfSense lfSense, ILexEntry owner)
		{
			Guid guid = lfSense.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense fdoSense = GetOrCreateSenseByGuid(guid, owner);
			// TODO: Set instance fields

			// var converter = new PossibilityListConverter(Cache.LanguageProject.LocationsOA);
			// fdoPronunciation.LocationRA = (ICmLocation)converter.GetByName(lfEntry.Location.Value);
			// TODO: Check if the compiler is happy with the below (creating an object and throwing it away after calling one method)
			//			new PossibilityListConverter(Cache.LanguageProject.SemanticDomainListOA)
			//				.UpdatePossibilitiesFromStringArray(fdoSense.DomainTypesRC, lfSense.AcademicDomains);
			//			new PossibilityListConverter(Cache.LanguageProject.AnthroListOA)
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
			IPartOfSpeech pos = ConvertPos(lfSense.PartOfSpeech, lfSense);
			if (pos != null)
			{
				IPartOfSpeech secondaryPos = ConvertPos(lfSense.SecondaryPartOfSpeech, lfSense); // Only used in derivational affixes, will be null otherwise
				if (fdoSense.MorphoSyntaxAnalysisRA == null)
				{
					// If we've got a brand-new fdoSense object, we'll need to create a new MSA for it.
					// That's what the SandboxGenericMSA class is for: assigning it to the SandboxMSA
					// member of fdoSense will automatically create an MSA of the correct class. Handy!
					MsaType msaType = fdoSense.GetDesiredMsaType();
					SandboxGenericMSA sandboxMsa = SandboxGenericMSA.Create(msaType, pos);
					if (secondaryPos != null)
						sandboxMsa.SecondaryPOS = secondaryPos;
					fdoSense.SandboxMSA = sandboxMsa;
				}
				else
				{
					ConvertMongoToFdoPartsOfSpeech.SetPartOfSpeech(fdoSense.MorphoSyntaxAnalysisRA, pos, secondaryPos); // It's fine if secondaryPos is null
					Logger.Info("Part of speech of {0} has been set to {1}", fdoSense.MorphoSyntaxAnalysisRA.GetGlossOfFirstSense(), pos);
				}
			}
			// fdoSense.MorphoSyntaxAnalysisRA.MLPartOfSpeech = lfSense.PartOfSpeech; // TODO: FAR more complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.PhonologyNote, lfSense.PhonologyNote);
			foreach (LfPicture lfPicture in lfSense.Pictures)
				LfPictureToFdoPicture(lfPicture, fdoSense);
			// fdoSense.ReversalEntriesRC = lfSense.ReversalEntries; // TODO: More complex than that. Handle it correctly. Maybe.
			fdoSense.ScientificName = BestTsStringFromMultiText(lfSense.ScientificName);
			//			new PossibilityListConverter(Cache.LanguageProject.SemanticDomainListOA)
			//				.UpdatePossibilitiesFromStringArray(fdoSense.SemanticDomainsRC, lfSense.SemanticDomain);
			SetMultiStringFrom(fdoSense.SemanticsNote, lfSense.SemanticsNote);
			SetMultiStringFrom(fdoSense.Bibliography, lfSense.SenseBibliography);

			// lfSense.SenseId; // TODO: What do I do with this one?
			fdoSense.ImportResidue = BestTsStringFromMultiText(lfSense.SenseImportResidue);
			// fdoSense.PublishIn = lfSense.SensePublishIn; // TODO: More complex than that. Handle it correctly.
			SetMultiStringFrom(fdoSense.Restrictions, lfSense.SenseRestrictions);
			fdoSense.SenseTypeRA = new ConvertMongoToFdoPossibilityLists(Cache.LanguageProject.LexDbOA.SenseTypesOA, _wsEn).GetByKey(lfSense.SenseType);
			SetMultiStringFrom(fdoSense.SocioLinguisticsNote, lfSense.SociolinguisticsNote);
			fdoSense.Source = BestTsStringFromMultiText(lfSense.Source);
			// fdoSense.StatusRA = new PossibilityListConverter(Cache.LanguageProject.StatusOA).GetByName(lfSense.Status); // TODO: Nope, more complex.
			// fdoSense.UsageTypesRC = lfSense.Usages; // TODO: More complex than that. Handle it correctly.

			_customFieldConverter.SetCustomFieldsForThisCmObject(fdoSense, "senses", lfSense.CustomFields, lfSense.CustomFieldGuids);
		}

		public Guid GuidFromLiftId(string liftId)
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
		public void SetMultiStringFrom(IMultiStringAccessor dest, LfMultiText source)
		{
			if (source != null)
				source.WriteToFdoMultiString(dest, Cache.ServiceLocator.WritingSystemManager);
		}

		public void SetEtymologyFields(ILexEntry fdoEntry, LfLexEntry lfEntry)
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
				fdoEtymology.Source = BestStringFromMultiText(lfEntry.EtymologySource);
		}

		public void SetLexeme(ILexEntry fdoEntry, LfLexEntry lfEntry)
		{
			if (lfEntry.Lexeme == null)
				return;
			IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
			if (fdoLexeme == null)
				fdoLexeme = CreateOwnedLexemeForm(fdoEntry, lfEntry.MorphologyType); // Also sets owning field on fdoEntry
			SetMultiStringFrom(fdoLexeme.Form, lfEntry.Lexeme);
		}

		public void SetPronunciation(ILexEntry fdoEntry, LfLexEntry lfEntry)
		{
			if (lfEntry.Pronunciation == null &&
				lfEntry.CvPattern == null &&
				lfEntry.Tone == null &&
				lfEntry.Location == null)
			{
				// Do we even need to log this scenario? TODO: Either uncomment or remove the line below.
				// Logger.Info("No pronunciation data in lfEntry {0}", lfEntry.Guid);
				return;
			}
			ILexPronunciation fdoPronunciation = GetOrCreatePronunciationByGuid(lfEntry.PronunciationGuid, fdoEntry);

			fdoPronunciation.CVPattern = BestTsStringFromMultiText(lfEntry.CvPattern);
			fdoPronunciation.Tone = BestTsStringFromMultiText(lfEntry.Tone);
			SetMultiStringFrom(fdoPronunciation.Form, lfEntry.Pronunciation);
			if (lfEntry.Location != null)
			{
				// TODO: Cache this in the instance
				var converter = new ConvertMongoToFdoPossibilityLists(Cache.LanguageProject.LocationsOA, _wsEn);
				fdoPronunciation.LocationRA = (ICmLocation)converter.GetByName(lfEntry.Location.Value);
			}
			// Not handling fdoPronunciation.MediaFilesOS. TODO: At some point we may want to handle media files as well.
			// Not handling fdoPronunciation.LiftResidue
		}

		// TODO: Use a more generic ConvertMongoToFdoOptionList class, modeled after the corresponding Mongo->Fdo direction
		public void UpdateFdoGrammarFromLfGrammar(LfOptionList lfGrammar)
		{
			ICmPossibilityList fdoGrammar = Cache.LanguageProject.PartsOfSpeechOA;
			var posRepo = GetInstance<IPartOfSpeechRepository>();
			foreach (LfOptionListItem item in lfGrammar.Items)
			{
				OptionListItemToPartOfSpeech(item, fdoGrammar, posRepo);
			}
		}

		public IPartOfSpeech ConvertPos(LfStringField source, LfSense owner)
		{
			if (source == null || source.ToString() == null)
				return null;
			string posStr = source.ToString();
			LfOptionListItem lfGrammarEntry;
			if (_lfGrammarByKey.TryGetValue(posStr, out lfGrammarEntry))
				return OptionListItemToPartOfSpeech(lfGrammarEntry, Cache.LanguageProject.PartsOfSpeechOA, GetInstance<IPartOfSpeechRepository>());
			//string userWs = _servLoc.WritingSystemManager.GetStrFromWs(Cache.DefaultUserWs);
			string userWs = ProjectRecord.InterfaceLanguageCode;
			if (String.IsNullOrEmpty(userWs))
				userWs = "en";
			// return posConverter.FromAbbrevAndName(lfSense.PartOfSpeech.ToString(), userWs);
			Logger.Warning("Part of speech with key {0} (found in sense {1} with GUID {2}) has no corresponding entry in the {3} optionlist of project {4}. Falling back to creating an FDO part of speech from abbreviation {5}, which is not ideal.",
				posStr,
				owner.Gloss,
				(owner.Guid != null) ? owner.Guid.ToString() : "(no GUID)",
				MagicStrings.LfOptionListCodeForGrammaticalInfo,
				LfProject.LfProjectCode,
				posStr
			);
			return _posConverter.FromAbbrevAndName(posStr, null, userWs);
		}

		// TODO: This probably belongs in the ConvertMongoToFdoPartsOfSpeech class
		public IPartOfSpeech OptionListItemToPartOfSpeech(LfOptionListItem item, ICmPossibilityList posList, IPartOfSpeechRepository posRepo)
		{
			IPartOfSpeech pos = null;
			if (item.Guid != null)
			{
				if (posRepo.TryGetObject(item.Guid.Value, out pos))
				{
					// Any fields that are different need to be set in the FDO PoS object. ... WAIT. That's not true any more, is it? TODO: Examine this.
					pos.Abbreviation.SetAnalysisDefaultWritingSystem(item.Abbreviation);
					pos.Name.SetAnalysisDefaultWritingSystem(item.Value);
					// pos.Description won't be updated as that field is currently not kept in LF
					return pos;
				}
				else
				{
					// No pos with that GUID, so we might have to create one
					pos = _posConverter.FromAbbrevAndName(item.Key, item.Value, ProjectRecord.InterfaceLanguageCode);
					return pos;
				}
			}
			else
			{
				// Don't simply assume FDO doesn't know about it until we search by name and abbreviation.
				// LF PoS keys are English *only* and never translated. Try that first.
				pos = posList.FindPossibilityByName(posList.PossibilitiesOS, item.Key, _wsEn) as IPartOfSpeech;
				if (pos != null)
					return pos;
				// Part of speech name, though, should be searched in the LF analysis language
				// TODO: Using interface language as a fallback, but get the analysis language once it's available
				int wsId = Cache.WritingSystemFactory.GetWsFromStr(ProjectRecord.InterfaceLanguageCode);
				pos = posList.FindPossibilityByName(posList.PossibilitiesOS, item.Value, wsId) as IPartOfSpeech;
				if (pos != null)
					return pos;
				// If we still haven't found it, we'll need to create one
				pos = _posConverter.FromAbbrevAndName(item.Key, item.Value, ProjectRecord.InterfaceLanguageCode);
				return pos;
			}
		}
	}
}

