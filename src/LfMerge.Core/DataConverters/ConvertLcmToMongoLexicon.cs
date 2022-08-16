// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using MongoDB.Bson;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.Progress;

namespace LfMerge.Core.DataConverters
{
	public class ConvertLcmToMongoLexicon
	{
		private ILfProject LfProject { get; set; }
		private FwProject FwProject { get; set; }
		private LcmCache Cache { get; set; }
		private IProgress Progress { get; set; }
		private FwServiceLocatorCache ServiceLocator { get; set; }
		private ILogger Logger { get; set; }
		private IMongoConnection Connection { get; set; }
		private MongoProjectRecordFactory ProjectRecordFactory { get; set; }


		private int _wsEn;

		private ConvertLcmToMongoCustomField _convertCustomField;

		// Shorter names to use in this class since MagicStrings.LfOptionListCodeForGrammaticalInfo (etc.) are real mouthfuls
		private const string GrammarListCode = MagicStrings.LfOptionListCodeForGrammaticalInfo;
		private const string SemDomListCode = MagicStrings.LfOptionListCodeForSemanticDomains;
		private const string AcademicDomainListCode = MagicStrings.LfOptionListCodeForAcademicDomainTypes;
//		private const string EnvironListCode = MagicStrings.LfOptionListCodeForEnvironments;  // Skip since we're not currently converting this (LF data model is too different)
		private const string LocationListCode = MagicStrings.LfOptionListCodeForLocations;
		private const string UsageTypeListCode = MagicStrings.LfOptionListCodeForUsageTypes;
//		private const string ReversalTypeListCode = MagicStrings.LfOptionListCodeForReversalTypes;  // Skip since we're not currently converting this (LF data model is too different)
		private const string SenseTypeListCode = MagicStrings.LfOptionListCodeForSenseTypes;
		private const string AnthroCodeListCode = MagicStrings.LfOptionListCodeForAnthropologyCodes;
		private const string StatusListCode = MagicStrings.LfOptionListCodeForStatus;

		private IDictionary<string, ConvertLcmToMongoOptionList> ListConverters;

		//private ConvertLcmToMongoOptionList _convertAnthroCodesOptionList;

		public ConvertLcmToMongoLexicon(ILfProject lfProject, ILogger logger, IMongoConnection connection, IProgress progress, MongoProjectRecordFactory projectRecordFactory)
		{
			LfProject = lfProject;
			Logger = logger;
			Connection = connection;
			Progress = progress;
			ProjectRecordFactory = projectRecordFactory;

			FwProject = LfProject.FieldWorksProject;
			Cache = FwProject.Cache;
			ServiceLocator = FwProject.ServiceLocator;
			_wsEn = ServiceLocator.WritingSystemFactory.GetWsFromStr("en");

			// Reconcile writing systems from Lcm and Mongo
			Dictionary<string, LfInputSystemRecord> lfWsList = LcmWsToLfWs();
			#if FW8_COMPAT
			List<string> VernacularWss = ServiceLocator.LanguageProject.CurrentVernacularWritingSystems.Select(ws => ws.Id).ToList();
			List<string> AnalysisWss = ServiceLocator.LanguageProject.CurrentAnalysisWritingSystems.Select(ws => ws.Id).ToList();
			List<string> PronunciationWss = ServiceLocator.LanguageProject.CurrentPronunciationWritingSystems.Select(ws => ws.Id).ToList();
			#else
			List<string> VernacularWss = ServiceLocator.LanguageProject.CurrentVernacularWritingSystems.Select(ws => ws.LanguageTag).ToList();
			List<string> AnalysisWss = ServiceLocator.LanguageProject.CurrentAnalysisWritingSystems.Select(ws => ws.LanguageTag).ToList();
			List<string> PronunciationWss = ServiceLocator.LanguageProject.CurrentPronunciationWritingSystems.Select(ws => ws.LanguageTag).ToList();
			#endif
			Connection.SetInputSystems(LfProject, lfWsList, VernacularWss, AnalysisWss, PronunciationWss);

			ListConverters = new Dictionary<string, ConvertLcmToMongoOptionList>();
			ListConverters[GrammarListCode] = ConvertOptionListFromLcm(LfProject, GrammarListCode, ServiceLocator.LanguageProject.PartsOfSpeechOA);
			ListConverters[SemDomListCode] = ConvertOptionListFromLcm(LfProject, SemDomListCode, ServiceLocator.LanguageProject.SemanticDomainListOA, updateMongoList: false);
			ListConverters[AcademicDomainListCode] = ConvertOptionListFromLcm(LfProject, AcademicDomainListCode, ServiceLocator.LanguageProject.LexDbOA.DomainTypesOA);
			ListConverters[LocationListCode] = ConvertOptionListFromLcm(LfProject, LocationListCode, ServiceLocator.LanguageProject.LocationsOA);
			ListConverters[UsageTypeListCode] = ConvertOptionListFromLcm(LfProject, UsageTypeListCode, ServiceLocator.LanguageProject.LexDbOA.UsageTypesOA);
			ListConverters[SenseTypeListCode] = ConvertOptionListFromLcm(LfProject, SenseTypeListCode, ServiceLocator.LanguageProject.LexDbOA.SenseTypesOA);
			ListConverters[AnthroCodeListCode] = ConvertOptionListFromLcm(LfProject, AnthroCodeListCode, ServiceLocator.LanguageProject.AnthroListOA);
			ListConverters[StatusListCode] = ConvertOptionListFromLcm(LfProject, StatusListCode, ServiceLocator.LanguageProject.StatusOA);

			_convertCustomField = new ConvertLcmToMongoCustomField(Cache, ServiceLocator, logger);
			foreach (KeyValuePair<string, ICmPossibilityList> pair in _convertCustomField.GetCustomFieldParentLists())
			{
				string listCode = pair.Key;
				ICmPossibilityList parentList = pair.Value;
				if (!ListConverters.ContainsKey(listCode))
					ListConverters[listCode] = ConvertOptionListFromLcm(LfProject, listCode, parentList);
			}
		}

		public ConversionError<ILexEntry> RunConversion()
		{
			var exceptions = new ConversionError<ILexEntry>();
			Logger.Notice("LcmToMongo: Converting lexicon for project {0}", LfProject.ProjectCode);
			ILexEntryRepository repo = GetInstance<ILexEntryRepository>();
			if (repo == null)
			{
				Logger.Error("Can't find LexEntry repository for FieldWorks project {0}", LfProject.ProjectCode);
				return exceptions;
			}

			// Custom field configuration AND view configuration should all be set at once
			Dictionary<string, LfConfigFieldBase> lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			Dictionary<string, string> lfCustomFieldTypes = new Dictionary<string, string>();
			_convertCustomField.WriteCustomFieldConfig(lfCustomFieldList, lfCustomFieldTypes);
			Connection.SetCustomFieldConfig(LfProject, lfCustomFieldList, lfCustomFieldTypes);

			Dictionary<Guid, DateTime> previousModificationDates = Connection.GetAllModifiedDatesForEntries(LfProject);

			int i = 1;
			foreach (ILexEntry LcmEntry in repo.AllInstances())
			{
				bool createdEntry = false;
				DateTime previousDateModified;
				if (!previousModificationDates.TryGetValue(LcmEntry.Guid, out previousDateModified))
				{
					// Looks like this entry is new in Lcm
					createdEntry = true;
					previousDateModified = DateTime.MinValue; // Ensure it will seem modified when comparing it later
				}
				// Remember that Lcm's DateModified is stored in local time for some incomprehensible reason...
				if (!createdEntry && previousDateModified.ToLocalTime() == LcmEntry.DateModified)
				{
					// Hasn't been modified since last time: just skip this record entirely
					continue;
				}

				try
				{
					LfLexEntry lfEntry = LcmLexEntryToLfLexEntry(LcmEntry);
					lfEntry.IsDeleted = false;
					Logger.Info("{3} - LcmToMongo: {0} LfEntry {1} ({2})", createdEntry ? "Created" : "Modified", lfEntry.Guid, ConvertUtilities.EntryNameForDebugging(lfEntry), i);
					Connection.UpdateRecord(LfProject, lfEntry);
				}
				catch (Exception e)
				{
					exceptions.AddEntryError(LcmEntry, e);
				}
				finally
				{
					i++;
				}
			}

			LfProject.IsInitialClone = false;

			RemoveMongoEntriesDeletedInLcm();
			// Logger.Debug("Running FtMComments, should see comments show up below:");
			var commCvtr = new ConvertLcmToMongoComments(Connection, LfProject, exceptions, Logger, Progress, ProjectRecordFactory);
			var commExceptions = commCvtr.RunConversion();
			exceptions.AddCommentErrors(commExceptions);
			return exceptions;
		}

		private void RemoveMongoEntriesDeletedInLcm()
		{
			IEnumerable<LfLexEntry> lfEntries = Connection.GetRecords<LfLexEntry>(LfProject, MagicStrings.LfCollectionNameForLexicon);
			foreach (LfLexEntry lfEntry in lfEntries)
			{
				if (lfEntry.Guid == null)
					continue;
				if (!ServiceLocator.ObjectRepository.IsValidObjectId(lfEntry.Guid.Value) ||
				    !ServiceLocator.ObjectRepository.GetObject(lfEntry.Guid.Value).IsValidObject)
				{
					if (lfEntry.IsDeleted)
						// Don't need to delete this record twice
						continue;

					lfEntry.IsDeleted = true;
					lfEntry.DateModified = DateTime.UtcNow;
					Logger.Info("LcmToMongo: Deleted LfEntry {0} ({1})", lfEntry.Guid, ConvertUtilities.EntryNameForDebugging(lfEntry));
					Connection.UpdateRecord(LfProject, lfEntry);
				}
			}
		}

		// Shorthand for getting an instance from the cache's service locator
		private T GetInstance<T>() where T : class
		{
			return ServiceLocator.GetInstance<T>();
		}

		private LfMultiText ToMultiText(IMultiAccessorBase LcmMultiString)
		{
			if (LcmMultiString == null) return null;
			return LfMultiText.FromLcmMultiString(LcmMultiString, ServiceLocator.WritingSystemManager);
		}

		public static LfMultiText ToMultiText(IMultiAccessorBase LcmMultiString, ILgWritingSystemFactory LcmWritingSystemManager)
		{
			if ((LcmMultiString == null) || (LcmWritingSystemManager == null)) return null;
			return LfMultiText.FromLcmMultiString(LcmMultiString, LcmWritingSystemManager);
		}

		private LfStringField ToStringField(string listCode, ICmPossibility LcmPoss)
		{
			return LfStringField.FromString(ListConverters[listCode].LfItemKeyString(LcmPoss, _wsEn));
		}

		private LfStringArrayField ToStringArrayField(string listCode, IEnumerable<ICmPossibility> LcmPossCollection)
		{
			return LfStringArrayField.FromStrings(ListConverters[listCode].LfItemKeyStrings(LcmPossCollection, _wsEn));
		}

		// Special case: LF sense Status field is a StringArray, but Lcm sense status is single possibility
		private LfStringArrayField ToStringArrayField(string listCode, ICmPossibility LcmPoss)
		{
			return LfStringArrayField.FromSingleString(ListConverters[listCode].LfItemKeyString(LcmPoss, _wsEn));
		}

		/// <summary>
		/// Convert Lcm lex entry to LF lex entry.
		/// </summary>
		/// <returns>LF entry
		/// <param name="LcmEntry">Lcm entry.</param>
		private LfLexEntry LcmLexEntryToLfLexEntry(ILexEntry LcmEntry)
		{
			if (LcmEntry == null) return null;

			ILgWritingSystem AnalysisWritingSystem = ServiceLocator.LanguageProject.DefaultAnalysisWritingSystem;
			ILgWritingSystem VernacularWritingSystem = ServiceLocator.LanguageProject.DefaultVernacularWritingSystem;

			var lfEntry = new LfLexEntry();

			IMoForm LcmLexeme = LcmEntry.LexemeFormOA;
			if (LcmLexeme == null)
				lfEntry.Lexeme = null;
			else
				lfEntry.Lexeme = ToMultiText(LcmLexeme.Form);
			// Other fields of LcmLexeme (AllomorphEnvironments, LiftResidue, MorphTypeRA, etc.) not mapped

			// Fields below in alphabetical order by ILexSense property, except for Lexeme
			foreach (IMoForm allomorph in LcmEntry.AlternateFormsOS)
			{
				// Do nothing; LanguageForge doesn't currently handle allomorphs, so we don't convert them
			}
			lfEntry.EntryBibliography = ToMultiText(LcmEntry.Bibliography);
			// TODO: Consider whether to use LcmEntry.CitationFormWithAffixType instead
			// (which would produce "-s" instead of "s" for the English plural suffix, for instance)
			lfEntry.CitationForm = ToMultiText(LcmEntry.CitationForm);
			lfEntry.Note = ToMultiText(LcmEntry.Comment);

			// DateModified and DateCreated can be confusing, because LF and Lcm are doing two different
			// things with them. In Lcm, there is just one DateModified and one DateCreated; simple. But
			// in LF, there is an AuthorInfo record as well, which contains its own ModifiedDate and CreatedDate
			// fields. (Note the word order: there's LfEntry.DateCreated, and LfEntry.AuthorInfo.CreatedDate).

			// The conversion we have chosen to use is: AuthorInfo will correspond to Lcm. So Lcm.DateCreated
			// becomes AuthorInfo.CreatedDate, and Lcm.DateModified becomes AuthorInfo.ModifiedDate. The two
			// fields on the LF entry will instead refer to when the *Mongo record* was created or modified,
			// and the LfEntry.DateCreated and LfEntry.DateModified fields will never be put into Lcm.

			var now = DateTime.UtcNow;
			if (LfProject.IsInitialClone)
			{
				lfEntry.DateCreated = now;
			}
			// LanguageForge needs this modified to know there is changed data
			lfEntry.DateModified = now;

			if (lfEntry.AuthorInfo == null)
				lfEntry.AuthorInfo = new LfAuthorInfo();
			lfEntry.AuthorInfo.CreatedByUserRef = null;
			lfEntry.AuthorInfo.CreatedDate = LcmEntry.DateCreated.ToUniversalTime();
			lfEntry.AuthorInfo.ModifiedByUserRef = null;
			lfEntry.AuthorInfo.ModifiedDate = LcmEntry.DateModified.ToUniversalTime();

#if DBVERSION_7000068
			ILexEtymology LcmEtymology = LcmEntry.EtymologyOA;
#else
			// TODO: Once LF's data model is updated from a single etymology to an array,
			// convert all of them instead of just the first. E.g.,
			// foreach (ILexEtymology LcmEtymology in LcmEntry.EtymologyOS) { ... }
			ILexEtymology LcmEtymology = null;
			if (LcmEntry.EtymologyOS.Count > 0)
				LcmEtymology = LcmEntry.EtymologyOS.First();
#endif
			if (LcmEtymology != null)
			{
				lfEntry.Etymology = ToMultiText(LcmEtymology.Form);
				lfEntry.EtymologyComment = ToMultiText(LcmEtymology.Comment);
				lfEntry.EtymologyGloss = ToMultiText(LcmEtymology.Gloss);
#if DBVERSION_7000068
				lfEntry.EtymologySource = LfMultiText.FromSingleStringMapping(AnalysisWritingSystem.Id, LcmEtymology.Source);
#else
				lfEntry.EtymologySource = ToMultiText(LcmEtymology.LanguageNotes);
#endif
				// LcmEtymology.LiftResidue not mapped
			}
			lfEntry.Guid = LcmEntry.Guid;
			if (LcmEntry.LIFTid == null)
			{
				lfEntry.LiftId = null;
			}
			else
			{
				lfEntry.LiftId = LcmEntry.LIFTid.Normalize(System.Text.NormalizationForm.FormC);  // Because LIFT files on disk are NFC and we need to make sure LiftIDs match those on disk
			}
			lfEntry.LiteralMeaning = ToMultiText(LcmEntry.LiteralMeaning);
			if (LcmEntry.PrimaryMorphType != null) {
				lfEntry.MorphologyType = LcmEntry.PrimaryMorphType.NameHierarchyString;
			}
			// TODO: Once LF's data model is updated from a single pronunciation to an array of pronunciations, convert all of them instead of just the first. E.g.,
			//foreach (ILexPronunciation LcmPronunciation in LcmEntry.PronunciationsOS) { ... }
			if (LcmEntry.PronunciationsOS.Count > 0)
			{
				ILexPronunciation LcmPronunciation = LcmEntry.PronunciationsOS.First();
				lfEntry.Pronunciation = ToMultiText(LcmPronunciation.Form);
				lfEntry.CvPattern = LfMultiText.FromSingleITsString(LcmPronunciation.CVPattern, ServiceLocator.WritingSystemFactory);
				lfEntry.Tone = LfMultiText.FromSingleITsString(LcmPronunciation.Tone, ServiceLocator.WritingSystemFactory);
				// TODO: Map LcmPronunciation.MediaFilesOS properly (converting video to sound files if necessary)
				lfEntry.Location = ToStringField(LocationListCode, LcmPronunciation.LocationRA);
			}
			lfEntry.EntryRestrictions = ToMultiText(LcmEntry.Restrictions);
			if (lfEntry.Senses == null) // Shouldn't happen, but let's be careful
				lfEntry.Senses = new List<LfSense>();
			lfEntry.Senses.AddRange(LcmEntry.SensesOS.Select(LcmSenseToLfSense));
			lfEntry.SummaryDefinition = ToMultiText(LcmEntry.SummaryDefinition);

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(LcmEntry, "entry", ListConverters);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfEntry.CustomFields = customFieldsBson;
			lfEntry.CustomFieldGuids = customFieldGuids;

			return lfEntry;

			/* Fields not mapped because it doesn't make sense to map them (e.g., Hvo, backreferences, etc):
			LcmEntry.ComplexFormEntries;
			LcmEntry.ComplexFormEntryRefs;
			LcmEntry.ComplexFormsNotSubentries;
			LcmEntry.EntryRefsOS;
			LcmEntry.HasMoreThanOneSense;
			LcmEntry.HeadWord; // Read-only virtual property
			LcmEntry.IsMorphTypesMixed; // Read-only property
			LcmEntry.LexEntryReferences;
			LcmEntry.MainEntriesOrSensesRS;
			LcmEntry.MinimalLexReferences;
			LcmEntry.MorphoSyntaxAnalysesOC;
			LcmEntry.MorphTypes;
			LcmEntry.NumberOfSensesForEntry;
			LcmEntry.PicturesOfSenses;

			*/

			/* Fields that would make sense to map, but that we don't because LF doesn't handle them (e.g., allomorphs):
			LcmEntry.AllAllomorphs; // LF doesn't handle allomorphs, so skip all allomorph-related fields
			LcmEntry.AlternateFormsOS;
			LcmEntry.CitationFormWithAffixType; // Citation form already mapped
			LcmEntry.DoNotPublishInRC;
			LcmEntry.DoNotShowMainEntryInRC;
			LcmEntry.DoNotUseForParsing;
			LcmEntry.HomographForm;
			LcmEntry.HomographFormKey;
			LcmEntry.HomographNumber;
			LcmEntry.ImportResidue;
			LcmEntry.LiftResidue;
			LcmEntry.PronunciationsOS
			LcmEntry.PublishAsMinorEntry;
			LcmEntry.PublishIn;
			LcmEntry.ShowMainEntryIn;
			LcmEntry.Subentries;
			LcmEntry.VariantEntryRefs;
			LcmEntry.VariantFormEntries;
			LcmEntry.VisibleComplexFormBackRefs;
			LcmEntry.VisibleComplexFormEntries;
			LcmEntry.VisibleVariantEntryRefs;

			*/
		}

		/// <summary>
		/// Convert Lcm sense to LF sense.
		/// </summary>
		/// <returns>LF sense
		/// <param name="lcmSense">Lcm sense.</param>
		private LfSense LcmSenseToLfSense(ILexSense lcmSense)
		{
			var lfSense = new LfSense();

			ILgWritingSystem VernacularWritingSystem = ServiceLocator.LanguageProject.DefaultVernacularWritingSystem;
			ILgWritingSystem AnalysisWritingSystem = ServiceLocator.LanguageProject.DefaultAnalysisWritingSystem;

			lfSense.Guid = lcmSense.Guid;
			lfSense.Gloss = ToMultiText(lcmSense.Gloss);
			lfSense.Definition = ToMultiText(lcmSense.Definition);

			// Fields below in alphabetical order by ILexSense property, except for Guid, Gloss and Definition
			lfSense.AcademicDomains = ToStringArrayField(AcademicDomainListCode, lcmSense.DomainTypesRC);
			lfSense.AnthropologyCategories = ToStringArrayField(AnthroCodeListCode, lcmSense.AnthroCodesRC);
			lfSense.AnthropologyNote = ToMultiText(lcmSense.AnthroNote);
			lfSense.DiscourseNote = ToMultiText(lcmSense.DiscourseNote);
			lfSense.EncyclopedicNote = ToMultiText(lcmSense.EncyclopedicInfo);
			if (lcmSense.ExamplesOS != null)
			{
				lfSense.Examples = new List<LfExample>(lcmSense.ExamplesOS.Select(LcmExampleToLfExample));
			}

			lfSense.GeneralNote = ToMultiText(lcmSense.GeneralNote);
			lfSense.GrammarNote = ToMultiText(lcmSense.GrammarNote);
			if (lcmSense.LIFTid == null)
			{
				lfSense.LiftId = null;
			}
			else
			{
				lfSense.LiftId = lcmSense.LIFTid.Normalize(System.Text.NormalizationForm.FormC);  // Because LIFT files on disk are NFC and we need to make sure LiftIDs match those on disk
			}
			if (lcmSense.MorphoSyntaxAnalysisRA != null)
			{
				IPartOfSpeech secondaryPos = null; // Only used in derivational affixes
				IPartOfSpeech pos = ConvertLcmToMongoPartsOfSpeech.FromMSA(lcmSense.MorphoSyntaxAnalysisRA, out secondaryPos);
				// Sometimes the part of speech can be null for legitimate reasons, so check the known class IDs before warning of an unknown MSA type
				if (pos == null && !ConvertLcmToMongoPartsOfSpeech.KnownMsaClassIds.Contains(lcmSense.MorphoSyntaxAnalysisRA.ClassID))
					Logger.Warning("Got MSA of unknown type {0} in sense {1} in project {2}",
						lcmSense.MorphoSyntaxAnalysisRA.GetType().Name,
						lcmSense.Guid,
						LfProject.ProjectCode);
				else
				{
					lfSense.PartOfSpeech = ToStringField(GrammarListCode, pos);
					lfSense.SecondaryPartOfSpeech = ToStringField(GrammarListCode, secondaryPos); // It's fine if secondaryPos is still null here
				}
			}
			lfSense.PhonologyNote = ToMultiText(lcmSense.PhonologyNote);
			if (lcmSense.PicturesOS != null)
			{
				lfSense.Pictures = new List<LfPicture>(lcmSense.PicturesOS.Select(LcmPictureToLfPicture));
				//Use the commented code for debugging into LcmPictureToLfPicture
				//
				//lfSense.Pictures = new List<LfPicture>();
				//foreach (var LcmPic in lcmSense.PicturesOS)
				//	lfSense.Pictures.Add(LcmPictureToLfPicture(LcmPic));
			}
			lfSense.SenseBibliography = ToMultiText(lcmSense.Bibliography);
			lfSense.SenseRestrictions = ToMultiText(lcmSense.Restrictions);

			if (lcmSense.ReferringReversalIndexEntries != null)
			{
				IEnumerable<string> reversalEntries = lcmSense.ReferringReversalIndexEntries.Select(lcmReversalEntry => lcmReversalEntry.LongName);
				lfSense.ReversalEntries = LfStringArrayField.FromStrings(reversalEntries);
			}
			lfSense.ScientificName = LfMultiText.FromSingleITsString(lcmSense.ScientificName, ServiceLocator.WritingSystemFactory);
			lfSense.SemanticDomain = ToStringArrayField(SemDomListCode, lcmSense.SemanticDomainsRC);
			lfSense.SemanticsNote = ToMultiText(lcmSense.SemanticsNote);
			// lcmSense.SensesOS; // Not mapped because LF doesn't handle subsenses. TODO: When LF handles subsenses, map this one.
			lfSense.SenseType = ToStringField(SenseTypeListCode, lcmSense.SenseTypeRA);
			lfSense.SociolinguisticsNote = ToMultiText(lcmSense.SocioLinguisticsNote);
			if (lcmSense.Source != null)
			{
				lfSense.Source = LfMultiText.FromSingleITsString(lcmSense.Source, ServiceLocator.WritingSystemFactory);
			}
			lfSense.Status = ToStringArrayField(StatusListCode, lcmSense.StatusRA);
			lfSense.Usages = ToStringArrayField(UsageTypeListCode, lcmSense.UsageTypesRC);


			/* Fields not mapped because it doesn't make sense to map them (e.g., Hvo, backreferences, etc):
			lcmSense.AllOwnedObjects;
			lcmSense.AllSenses;
			lcmSense.Cache;
			lcmSense.CanDelete;
			lcmSense.ChooserNameTS;
			lcmSense.ClassID;
			lcmSense.ClassName;
			lcmSense.Entry;
			lcmSense.EntryID;
			lcmSense.FullReferenceName;
			lcmSense.GetDesiredMsaType();
			lcmSense.Hvo;
			lcmSense.ImportResidue;
			lcmSense.IndexInOwner;
			lcmSense.IsValidObject;
			lcmSense.LexSenseReferences;
			lcmSense.LongNameTSS;
			lcmSense.ObjectIdName;
			lcmSense.OwnedObjects;
			lcmSense.Owner;
			lcmSense.OwningFlid;
			lcmSense.OwnOrd;
			lcmSense.ReferringObjects;
			lcmSense.ReversalNameForWs(wsVern);
			lcmSense.SandboxMSA; // Set-only property
			lcmSense.Self;
			lcmSense.Services;
			lcmSense.ShortName;
			lcmSense.ShortNameTSS;
			lcmSense.SortKey;
			lcmSense.SortKey2;
			lcmSense.SortKey2Alpha;
			lcmSense.SortKeyWs;
			lcmSense.VariantFormEntryBackRefs;
			lcmSense.VisibleComplexFormBackRefs;
			*/

			/* Fields not mapped because LanguageForge doesn't handle that data:
			lcmSense.AppendixesRC;
			lcmSense.ComplexFormEntries;
			lcmSense.ComplexFormsNotSubentries;
			lcmSense.DoNotPublishInRC;
			lcmSense.Subentries;
			lcmSense.ThesaurusItemsRC;
			lcmSense.LiftResidue;
			lcmSense.LexSenseOutline;
			lcmSense.PublishIn;
			*/

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(lcmSense, "senses", ListConverters);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			// TODO: Role Views only set on initial clone
			if (LfProject.IsInitialClone)
			{
				;
			}

			// If custom field was deleted in Flex, delete config here


			lfSense.CustomFields = customFieldsBson;
			lfSense.CustomFieldGuids = customFieldGuids;

			return lfSense;
		}

		/// <summary>
		/// Convert Lcm example LF example.
		/// </summary>
		/// <returns>LF example
		/// <param name="LcmExample">Lcm example.</param>
		private LfExample LcmExampleToLfExample(ILexExampleSentence LcmExample)
		{
			var lfExample = new LfExample();

			ILgWritingSystem AnalysisWritingSystem = ServiceLocator.LanguageProject.DefaultAnalysisWritingSystem;
			ILgWritingSystem VernacularWritingSystem = ServiceLocator.LanguageProject.DefaultVernacularWritingSystem;

			lfExample.Guid = LcmExample.Guid;
			lfExample.Sentence = ToMultiText(LcmExample.Example);
			lfExample.Reference = LfMultiText.FromSingleITsString(LcmExample.Reference, ServiceLocator.WritingSystemFactory);
			// ILexExampleSentence fields we currently do not convert:
			// LcmExample.DoNotPublishInRC;
			// LcmExample.LiftResidue;
			// LcmExample.PublishIn;

			// NOTE: Currently, LanguageForge only stores one translation per example, whereas Lcm can store
			// multiple translations with (possibly) different statuses (as freeform strings, like "old", "updated",
			// "needs fixing"...). Until LanguageForge acquires a data model where translations are stored in a list,
			// we will save only the first translation (if any) to Mongo. We also save the GUID so that the Mongo->Lcm
			// direction will know which ICmTranslation object to update with any changes.
			// TODO: Once LF improves its data model for translations, persist all of them instead of just the first.
			foreach (ICmTranslation translation in LcmExample.TranslationsOC.Take(1))
			{
				lfExample.Translation = ToMultiText(translation.Translation);
				lfExample.TranslationGuid = translation.Guid;
			}

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(LcmExample, "examples", ListConverters);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfExample.CustomFields = customFieldsBson;
			lfExample.CustomFieldGuids = customFieldGuids;
			return lfExample;
		}

		private LfPicture LcmPictureToLfPicture(ICmPicture LcmPicture)
		{
			var result = new LfPicture();
			result.Caption = ToMultiText(LcmPicture.Caption);
			if ((LcmPicture.PictureFileRA != null) && (!string.IsNullOrEmpty(LcmPicture.PictureFileRA.InternalPath)))
			{
				result.FileName = LcmPictureFilenameToLfPictureFilename(LcmPicture.PictureFileRA.InternalPath);
			}
			result.Guid = LcmPicture.Guid;
			// Unmapped ICmPicture fields include:
			// LcmPicture.Description;
			// LcmPicture.LayoutPos;
			// LcmPicture.LocationMax;
			// LcmPicture.LocationMin;
			// LcmPicture.LocationRangeType;
			// LcmPicture.ScaleFactor;
			return result;
		}

		private static string LcmPictureFilenameToLfPictureFilename(string LcmInternalFilename)
		{
			// Remove "Pictures" directory from internal path name
			// If the incoming internal path doesn't begin with "Pictures", then preserve the full external path.
			return Regex.Replace(LcmInternalFilename.Normalize(System.Text.NormalizationForm.FormC), @"^Pictures[/\\]", "");
		}

		/// <summary>
		/// Converts Lcm writing systems to LF input systems
		/// </summary>
		/// <returns>The list of LF input systems.</returns>
		private Dictionary<string, LfInputSystemRecord> LcmWsToLfWs()
		{
			// Using var here so that we'll stay compatible with both FW 8 and 9 (the type of these two lists changed between 8 and 9).
			var vernacularWSList = ServiceLocator.LanguageProject.CurrentVernacularWritingSystems;
			var analysisWSList = ServiceLocator.LanguageProject.CurrentAnalysisWritingSystems;

			var lfWsList = new Dictionary<string, LfInputSystemRecord>();
			foreach (var LcmWs in ServiceLocator.LanguageProject.AllWritingSystems)
			{
				var lfWs = new LfInputSystemRecord()
				{
					//These are for current libpalaso with SIL Writing Systems.
					// TODO: handle legacy WS definition
					Abbreviation = LcmWs.Abbreviation,
					IsRightToLeft = LcmWs.RightToLeftScript,
					LanguageName = LcmWs.LanguageName,
					#if FW8_COMPAT
					Tag = LcmWs.Id,
					#else
					Tag = LcmWs.LanguageTag,
					#endif
					VernacularWS = vernacularWSList.Contains(LcmWs),
					AnalysisWS = analysisWSList.Contains(LcmWs)
				};

				#if FW8_COMPAT
				lfWsList[LcmWs.Id] = lfWs;
				#else
				lfWsList[LcmWs.LanguageTag] = lfWs;
				#endif
			}
			return lfWsList;
		}

		private ConvertLcmToMongoOptionList ConvertOptionListFromLcm(ILfProject project, string listCode, ICmPossibilityList LcmOptionList, bool updateMongoList = true)
		{
			LfOptionList lfExistingOptionList = Connection.GetLfOptionListByCode(project, listCode);
			var converter = new ConvertLcmToMongoOptionList(lfExistingOptionList, _wsEn, listCode, Logger, ServiceLocator.WritingSystemFactory);
			LfOptionList lfChangedOptionList = converter.PrepareOptionListUpdate(LcmOptionList);
			if (updateMongoList)
				Connection.UpdateRecord(project, lfChangedOptionList, listCode);
			return new ConvertLcmToMongoOptionList(lfChangedOptionList, _wsEn, listCode, Logger, ServiceLocator.WritingSystemFactory);
		}
	}
}

