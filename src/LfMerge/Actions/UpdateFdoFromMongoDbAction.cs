// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.DataConverters;
using LfMerge.FieldWorks;
using LfMerge.Logging;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using MongoDB.Driver;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO.DomainServices;
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
		private LfOptionList _lfGrammar;
		private Dictionary<string, LfOptionListItem> _lfGrammarByKey;

		private ILexEntryRepository _entryRepo;
		private ILexExampleSentenceRepository _exampleRepo;
		private ICmPictureRepository _pictureRepo;
		private ILexPronunciationRepository _pronunciationRepo;
		private ILexSenseRepository _senseRepo;
		private IPartOfSpeechRepository _posRepo;
		private ICmTranslationRepository _translationRepo;
		private ILexEntryFactory _entryFactory;
		private ILexExampleSentenceFactory _exampleFactory;
		private ICmPictureFactory _pictureFactory;
		private ILexPronunciationFactory _pronunciationFactory;
		private ILexSenseFactory _senseFactory;
		private ICmTranslationFactory _translationFactory;

		private ICmPossibility _freeTranslationType; // Used in LfExampleToFdoExample(), but cached here

		private CustomFieldConverter _customFieldConverter;

		public UpdateFdoFromMongoDbAction(LfMergeSettingsIni settings, ILogger logger, IMongoConnection conn, MongoProjectRecordFactory factory) : base(settings, logger)
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
			Logger.Debug("FdoFromMongoDb: starting");
			_lfProject = project;
			_projectRecord = _projectRecordFactory.Create(_lfProject);
			if (_projectRecord == null)
			{
				Logger.Warning("No project named {0}", _lfProject.LfProjectCode);
				Logger.Warning("If we are unit testing, this may not be an error");
				return;
			}
			_lfProjectConfig = _projectRecord.Config;
			if (_lfProjectConfig == null)
				return;

			if (project.FieldWorksProject == null)
			{
				Logger.Error("Failed to find the corresponding FieldWorks project!");
				return;
			}
			if (project.FieldWorksProject.IsDisposed)
				Logger.Warning("Project {0} is already disposed; this shouldn't happen", project.FwProjectCode);
			_cache = project.FieldWorksProject.Cache;
			if (_cache == null)
			{
				Logger.Error("Failed to find the FDO cache!");
				return;
			}

			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Logger.Error("Failed to find the service locator; giving up.");
				return;
			}

			// Update writing systems from project config input systems
			LfWsToFdoWs(_projectRecord.InputSystems);

			_customFieldConverter = new CustomFieldConverter(_cache);

			// For efficiency's sake, cache the six repositories and six factories we'll need all the time,
			_entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			_exampleRepo = _servLoc.GetInstance<ILexExampleSentenceRepository>();
			_pictureRepo = _servLoc.GetInstance<ICmPictureRepository>();
			_pronunciationRepo = _servLoc.GetInstance<ILexPronunciationRepository>();
			_senseRepo = _servLoc.GetInstance<ILexSenseRepository>();
			_posRepo = _servLoc.GetInstance<IPartOfSpeechRepository>();
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

			_lfGrammar = GetGrammar(project);
			if (_lfGrammar == null)
				_lfGrammarByKey = new Dictionary<string, LfOptionListItem>();
			else
				_lfGrammarByKey = _lfGrammar.Items.ToDictionary(item => item.Key, item => item);
			/* Comment this logging out once we're sure the grammar conversion works */
			var grammarLogMsgs = new List<string>();
			grammarLogMsgs.Add("Grammar follows:");
			foreach (LfOptionListItem item in _lfGrammarByKey.Values)
			{
				grammarLogMsgs.Add(String.Format("Grammar item {0} has abbrev {1}, key {2} and GUID {3}",
					item.Value, item.Abbreviation, item.Key, (item.Guid == null) ? "(none)" : item.Guid.Value.ToString()
				));
			}
			Logger.LogMany(LogSeverity.Debug, grammarLogMsgs);
			/* */
			IEnumerable<LfLexEntry> lexicon = GetLexiconForTesting(project, _lfProjectConfig);
			NonUndoableUnitOfWorkHelper.Do(_cache.ActionHandlerAccessor, () =>
				{
					if (_lfGrammar != null)
						UpdateFdoGrammarFromLfGrammar(_lfGrammar);
					foreach (LfLexEntry lfEntry in lexicon)
						LfLexEntryToFdoLexEntry(lfEntry);
				});
			_cache.ActionHandlerAccessor.Commit();
			Logger.Debug("FdoFromMongoDb: done");
		}

		private IEnumerable<LfLexEntry> GetLexiconForTesting(ILfProject project, ILfProjectConfig config)
		{
			//			IMongoDatabase db = _connection.GetProjectDatabase(project);
			//			IMongoCollection<LfLexEntry> collection = db.GetCollection<LfLexEntry>("lexicon");
			//			IAsyncCursor<LfLexEntry> result = collection.Find<LfLexEntry>(_ => true).ToCursor();
			//			return result.AsEnumerable();
			return _connection.GetRecords<LfLexEntry>(project, MagicStrings.LfCollectionNameForLexicon);
		}

		private ILfProjectConfig GetConfigForTesting(ILfProject project)
		{
			// TODO: Pretty sure this function is unused; remove it if it really is unused.
			return _projectRecord.Config;
		}

		private IEnumerable<LfOptionList> GetOptionLists(ILfProject project)
		{
			return _connection.GetRecords<LfOptionList>(project, MagicStrings.LfCollectionNameForOptionLists);
		}

		private LfOptionList GetGrammar(ILfProject project)
		{
			return GetOptionLists(project).FirstOrDefault(x => x.Code == MagicStrings.LfOptionListCodeForGrammaticalInfo);
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

		/// <summary>
		/// Converts the list of LF input systems and adds them to FDO writing systems
		/// </summary>
		/// <param name="lfWsList">List of LF input systems.</param>
		private void LfWsToFdoWs(Dictionary<string, LfInputSystemRecord> lfWsList)
		{
			WritingSystemManager wsm = _servLoc.WritingSystemManager;
			if (wsm == null)
			{
				Logger.Error("Failed to find the writing system manager");
				return;
			}

			string vernacularLanguageCode = _projectRecord.LanguageCode;
			foreach (var lfWs in lfWsList.Values)
			{
				CoreWritingSystemDefinition ws;
				if (wsm.TryGet(lfWs.Tag, out ws))
				{
					ws.Abbreviation = lfWs.Abbreviation;
					ws.RightToLeftScript = lfWs.IsRightToLeft;
					// Creating LanguageSubTag throwing exceptions??
					// ws.Language = new SIL.WritingSystems.LanguageSubtag(lfWs.Tag, lfWs.LanguageName);
					ws.LanguageTag = lfWs.Tag;
					wsm.Replace(ws);
				}
				else
				{
					ws = wsm.Create(lfWs.Tag);
					ws.Abbreviation = lfWs.Abbreviation;
					ws.RightToLeftScript = lfWs.IsRightToLeft;
					// Creating LanguageSubTag throwing exceptions??
					// ws.Language = new SIL.WritingSystems.LanguageSubtag(lfWs.Tag, lfWs.LanguageName);

					// TODO: Also distinguish vernacular / analysis writing system  DDW 02-2016
					if (lfWs.Tag.Equals(vernacularLanguageCode))
					{
						_servLoc.WritingSystems.AddToCurrentVernacularWritingSystems(ws);
					}
					wsm.Set(ws);
				}
			}
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
				// Do we even need to log this scenario? TODO: Either uncomment or remove the line below.
				// Logger.Info("No pronunciation data in lfEntry {0}", lfEntry.Guid);
				return;
			}
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
				Logger.Warning("BestStringFromMultiText got a non-null multitext, but it was empty. Empty LF MultiText objects should be nulls in Mongo. Unfortunately, at this point in the code it's hard to know which multitext it was.");
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
					Logger.Info("Returning TsString from {0} for writing system {1}", field.Value, wsStr);
					return TsStringUtils.MakeTss(field.Value, wsId);
				}
			}

			// Last-ditch option: just grab the first non-empty string we can find
			KeyValuePair<int, string> kv = input.WsIdAndFirstNonEmptyString(_cache);
			if (kv.Value == null) return null;
			Logger.Info("Returning first non-empty TsString from {0} for writing system {1}", kv.Value, wsm.GetStrFromWs(kv.Key));
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
			Logger.Info("FDO Example just got set to {0} for GUID {1} and HVO {2}",
				fdoExample.Example.BestAnalysisVernacularAlternative.Text,
				fdoExample.Guid,
				fdoExample.Hvo
			);
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
			if (lfSense.PartOfSpeech != null && lfSense.PartOfSpeech.Value != null)
			{
				IPartOfSpeech pos = null;
				if (lfSense.PartOfSpeechGuid != null)
					pos = fdoSense.Cache.ServiceLocator.GetInstance<IPartOfSpeechRepository>().GetObject(lfSense.PartOfSpeechGuid.Value);
				if (pos == null) // GetObject() returns null if not found, which means we need to make a PoS object from the name
				{
					var posConverter = new PartOfSpeechConverter(_cache);
					//string userWs = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultUserWs);
					string userWs = _projectRecord.InterfaceLanguageCode;
					if (String.IsNullOrEmpty(userWs))
						userWs = "en";
					// pos = posConverter.FromAbbrevAndName(lfSense.PartOfSpeech.ToString(), userWs);
					string posStr = lfSense.PartOfSpeech.ToString();
					LfOptionListItem lfGrammarEntry;
					if (_lfGrammarByKey.TryGetValue(posStr, out lfGrammarEntry))
						pos = OptionListItemToPartOfSpeech(lfGrammarEntry, _cache.LanguageProject.PartsOfSpeechOA, _posRepo);
					else
					{
						Logger.Warning("Part of speech with key {0} (found in sense {1} with GUID {2}) has no corresponding entry in the {3} optionlist of project {4}. Falling back to creating an FDO part of speech from abbreviation {5}, which is not ideal.",
							posStr,
							lfSense.Gloss,
							(lfSense.Guid != null) ? lfSense.Guid.ToString() : "(no GUID)",
							MagicStrings.LfOptionListCodeForGrammaticalInfo,
							_lfProject.LfProjectCode,
							posStr
						);
						pos = posConverter.FromAbbrevAndName(posStr, null, userWs);
					}
				}
				if (pos != null) // TODO: If it's null, PartOfSpeechConverter.FromName will eventually create it. Once that happens, this check can be removed.
				{
					if (fdoSense.MorphoSyntaxAnalysisRA == null)
					{
						// If we've got a brand-new fdoSense object, we'll need to create a new MSA for it.
						// That's what the SandboxGenericMSA class is for: assigning it to the SandboxMSA
						// member of fdoSense will automatically create an MSA of the correct class. Handy!
						MsaType msaType = fdoSense.GetDesiredMsaType();
						SandboxGenericMSA sandboxMsa = SandboxGenericMSA.Create(msaType, pos);
						if (msaType == MsaType.kDeriv)
						{
							// Derivational affixes have a "From" *and* a "To" pos. "From" is the main pos,
							// but there's a secondary pos member in the sandbox MSA specifically for the "To"
							// pos of derivational affixes. Currently LF doesn't handle that, so we do nothing
							// with the secondary pos.
							// TODO: Once LF handles secondary / "To" parts of speech, do the right thing here.
							// sandboxMsa.SecondaryPOS = GetSecondaryPosFromLfSomehow();
						}
						fdoSense.SandboxMSA = sandboxMsa;
					}
					else
					{
						if (fdoSense.MorphoSyntaxAnalysisRA.ClassID == MoDerivAffMsaTags.kClassId)
						{
							// TODO: Turn this into a proper log message
							Logger.Warning("Sense {0} ({1}) is a derivational affix, which needs two parts of speech, From and To. Setting the From PoS to {2}, but not changing the To PoS. This might cause duplicated grammar analysis objects in FieldWorks.",
								fdoSense.Guid, fdoSense.Gloss.BestAnalysisVernacularAlternative.Text, pos.NameHierarchyString);
						}
						PartOfSpeechConverter.SetPartOfSpeech(fdoSense.MorphoSyntaxAnalysisRA, pos);
						Logger.Info("Part of speech of {0} has been set to {1}", fdoSense.MorphoSyntaxAnalysisRA.GetGlossOfFirstSense(), pos);
					}
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
				Logger.Warning("Unrecognized morphology type \"{0}\" in word {1}", morphologyType, owner.Guid);
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

		private void UpdateFdoGrammarFromLfGrammar(LfOptionList lfGrammar)
		{
			ICmPossibilityList fdoGrammar = _cache.LanguageProject.PartsOfSpeechOA;
			var posRepo = _servLoc.GetInstance<IPartOfSpeechRepository>();
			foreach (LfOptionListItem item in lfGrammar.Items)
			{
				IPartOfSpeech pos = OptionListItemToPartOfSpeech(item, fdoGrammar, posRepo);
				// TODO: Once we're confident that this works, remove this log message
				Logger.Info("Updated FDO grammar entry with PoS {0}", pos.AbbrAndName);
			}
		}

		private IPartOfSpeech OptionListItemToPartOfSpeech(LfOptionListItem item, ICmPossibilityList posList, IPartOfSpeechRepository posRepo)
		{
			IPartOfSpeech pos = null;
			int wsEn = _cache.WritingSystemFactory.GetWsFromStr("en");
			if (item.Guid != null)
			{
				if (posRepo.TryGetObject(item.Guid.Value, out pos))
				{
					// Any fields that are different need to be set in the FDO PoS object
					pos.Abbreviation.SetAnalysisDefaultWritingSystem(item.Abbreviation);
					pos.Name.SetAnalysisDefaultWritingSystem(item.Value);
					// pos.Description won't be updated as that field is currently not kept in LF
					return pos;
				}
				else
				{
					// No pos with that GUID, so we might have to create one
					var converter = new PartOfSpeechConverter(_cache);
					pos = converter.FromAbbrevAndName(item.Key, item.Value, _projectRecord.InterfaceLanguageCode);
					return pos;
				}
			}
			else
			{
				// Don't simply assume FDO doesn't know about it until we search by name and abbreviation.
				// LF PoS keys are English *only* and never translated. Try that first.
				pos = posList.FindPossibilityByName(posList.PossibilitiesOS, item.Key, wsEn) as IPartOfSpeech;
				if (pos != null)
					return pos;
				// Part of speech name, though, should be searched in the LF analysis language
				// TODO: Using interface language as a fallback, but get the analysis language once it's available
				int wsId = _cache.WritingSystemFactory.GetWsFromStr(_projectRecord.InterfaceLanguageCode);
				pos = posList.FindPossibilityByName(posList.PossibilitiesOS, item.Value, wsId) as IPartOfSpeech;
				if (pos != null)
					return pos;
				// If we still haven't found it, we'll need to create one
				var converter = new PartOfSpeechConverter(_cache);
				pos = converter.FromAbbrevAndName(item.Key, item.Value, _projectRecord.InterfaceLanguageCode);
				return pos;
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}
