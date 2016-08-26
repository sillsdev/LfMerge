// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using LfMerge;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using LfMerge.Logging;
using LfMerge.MongoConnector;
using MongoDB.Bson;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LfMerge.DataConverters
{
	public class ConvertFdoToMongoLexicon
	{
		public ILfProject LfProject { get; protected set; }
		public FwProject FwProject { get; protected set; }
		public FdoCache Cache { get; protected set; }
		public ILogger Logger { get; protected set; }
		public IMongoConnection Connection { get; protected set; }

		public int _wsEn;

		public ConvertFdoToMongoCustomField _convertCustomField;

		// Shorter names to use in this class since MagicStrings.LfOptionListCodeForGrammaticalInfo (etc.) are real mouthfuls
		public const string GrammarListCode = MagicStrings.LfOptionListCodeForGrammaticalInfo;
		public const string SemDomListCode = MagicStrings.LfOptionListCodeForSemanticDomains;
		public const string AcademicDomainListCode = MagicStrings.LfOptionListCodeForAcademicDomainTypes;
//		public const string EnvironListCode = MagicStrings.LfOptionListCodeForEnvironments;  // Skip since we're not currently converting this (LF data model is too different)
		public const string LocationListCode = MagicStrings.LfOptionListCodeForLocations;
		public const string UsageTypeListCode = MagicStrings.LfOptionListCodeForUsageTypes;
//		public const string ReversalTypeListCode = MagicStrings.LfOptionListCodeForReversalTypes;  // Skip since we're not currently converting this (LF data model is too different)
		public const string SenseTypeListCode = MagicStrings.LfOptionListCodeForSenseTypes;
		public const string AnthroCodeListCode = MagicStrings.LfOptionListCodeForAnthropologyCodes;
		public const string PublishInListCode = MagicStrings.LfOptionListCodeForDoNotPublishIn;
		public const string StatusListCode = MagicStrings.LfOptionListCodeForStatus;

		public IDictionary<string, ConvertFdoToMongoOptionList> ListConverters;

		public ConvertFdoToMongoOptionList _convertAnthroCodesOptionList;

		public ConvertFdoToMongoLexicon(ILfProject lfProject, ILogger logger, IMongoConnection connection)
		{
			LfProject = lfProject;
			Logger = logger;
			Connection = connection;

			FwProject = LfProject.FieldWorksProject;
			Cache = FwProject.Cache;
			_wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");

			// Reconcile writing systems from FDO and Mongo
			Dictionary<string, LfInputSystemRecord> lfWsList = FdoWsToLfWs();
			#if FW8_COMPAT
			List<string> VernacularWss = Cache.LanguageProject.CurrentVernacularWritingSystems.Select(ws => ws.Id).ToList();
			List<string> AnalysisWss = Cache.LanguageProject.CurrentAnalysisWritingSystems.Select(ws => ws.Id).ToList();
			List<string> PronunciationWss = Cache.LanguageProject.CurrentPronunciationWritingSystems.Select(ws => ws.Id).ToList();
			#else
			List<string> VernacularWss = Cache.LanguageProject.CurrentVernacularWritingSystems.Select(ws => ws.LanguageTag).ToList();
			List<string> AnalysisWss = Cache.LanguageProject.CurrentAnalysisWritingSystems.Select(ws => ws.LanguageTag).ToList();
			List<string> PronunciationWss = Cache.LanguageProject.CurrentPronunciationWritingSystems.Select(ws => ws.LanguageTag).ToList();
			#endif
			Connection.SetInputSystems(LfProject, lfWsList, VernacularWss, AnalysisWss, PronunciationWss);

			ListConverters = new Dictionary<string, ConvertFdoToMongoOptionList>();
			ListConverters[GrammarListCode] = ConvertOptionListFromFdo(LfProject, GrammarListCode, Cache.LanguageProject.PartsOfSpeechOA);
			ListConverters[SemDomListCode] = ConvertOptionListFromFdo(LfProject, SemDomListCode, Cache.LanguageProject.SemanticDomainListOA, updateMongoList: false);
			ListConverters[AcademicDomainListCode] = ConvertOptionListFromFdo(LfProject, AcademicDomainListCode, Cache.LanguageProject.LexDbOA.DomainTypesOA);
			ListConverters[LocationListCode] = ConvertOptionListFromFdo(LfProject, LocationListCode, Cache.LanguageProject.LocationsOA);
			ListConverters[UsageTypeListCode] = ConvertOptionListFromFdo(LfProject, UsageTypeListCode, Cache.LanguageProject.LexDbOA.UsageTypesOA);
			ListConverters[SenseTypeListCode] = ConvertOptionListFromFdo(LfProject, SenseTypeListCode, Cache.LanguageProject.LexDbOA.SenseTypesOA);
			ListConverters[AnthroCodeListCode] = ConvertOptionListFromFdo(LfProject, AnthroCodeListCode, Cache.LanguageProject.AnthroListOA);
			ListConverters[PublishInListCode] = ConvertOptionListFromFdo(LfProject, PublishInListCode, Cache.LanguageProject.LexDbOA.PublicationTypesOA);
			ListConverters[StatusListCode] = ConvertOptionListFromFdo(LfProject, StatusListCode, Cache.LanguageProject.StatusOA);

			_convertCustomField = new ConvertFdoToMongoCustomField(Cache, logger);
			foreach (KeyValuePair<string, ICmPossibilityList> pair in _convertCustomField.GetCustomFieldParentLists())
			{
				string listCode = pair.Key;
				ICmPossibilityList parentList = pair.Value;
				if (!ListConverters.ContainsKey(listCode))
					ListConverters[listCode] = ConvertOptionListFromFdo(LfProject, listCode, parentList);
			}
		}

		public void RunConversion()
		{
			Logger.Notice("FdoToMongo: Converting lexicon for project {0}", LfProject.ProjectCode);
			ILexEntryRepository repo = GetInstance<ILexEntryRepository>();
			if (repo == null)
			{
				Logger.Error("Can't find LexEntry repository for FieldWorks project {0}", LfProject.ProjectCode);
				return;
			}

			Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();
			Dictionary<Guid, DateTime> previousModificationDates = Connection.GetAllModifiedDatesForEntries(LfProject);

			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				LfLexEntry lfEntry = FdoLexEntryToLfLexEntry(fdoEntry, _lfCustomFieldList);
				string entryNameForDebugging;
				if (lfEntry.Lexeme == null)
					entryNameForDebugging = "<null lexeme>";
				else
					entryNameForDebugging = String.Join(", ", lfEntry.Lexeme.Values.Select(x => x.Value ?? ""));
				lfEntry.IsDeleted = false;
				if (lfEntry.Guid.HasValue)
				{
					DateTime lastModifiedDate;
					if (previousModificationDates.TryGetValue(lfEntry.Guid.Value, out lastModifiedDate))
					{
						if (lastModifiedDate != lfEntry.DateModified)
							lfEntry.DateModified = DateTime.UtcNow;
					}
				}
				Logger.Info("FdoToMongo: Converted LfEntry {0} ({1})", lfEntry.Guid, entryNameForDebugging);
				Connection.UpdateRecord(LfProject, lfEntry);
			}
			LfProject.IsInitialClone = false;

			RemoveMongoEntriesDeletedInFdo();

			Connection.SetCustomFieldConfig(LfProject, _lfCustomFieldList);
			_convertCustomField.CreateCustomFieldsConfigViews(LfProject, _lfCustomFieldList);
		}

		private void RemoveMongoEntriesDeletedInFdo()
		{
			IEnumerable<LfLexEntry> lfEntries = Connection.GetRecords<LfLexEntry>(LfProject, MagicStrings.LfCollectionNameForLexicon);
			foreach (LfLexEntry lfEntry in lfEntries)
			{
				if (lfEntry.Guid == null)
					continue;
				if (!Cache.ServiceLocator.ObjectRepository.IsValidObjectId(lfEntry.Guid.Value) ||
				    !Cache.ServiceLocator.ObjectRepository.GetObject(lfEntry.Guid.Value).IsValidObject)
				{
					lfEntry.IsDeleted = true;
					Connection.UpdateRecord(LfProject, lfEntry);
				}
			}
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

		public LfMultiText ToMultiText(IMultiAccessorBase fdoMultiString)
		{
			if (fdoMultiString == null) return null;
			return LfMultiText.FromFdoMultiString(fdoMultiString, Cache.ServiceLocator.WritingSystemManager);
		}

		static public LfMultiText ToMultiText(IMultiAccessorBase fdoMultiString, ILgWritingSystemFactory fdoWritingSystemManager)
		{
			if ((fdoMultiString == null) || (fdoWritingSystemManager == null)) return null;
			return LfMultiText.FromFdoMultiString(fdoMultiString, fdoWritingSystemManager);
		}

		public LfStringField ToStringField(string listCode, ICmPossibility fdoPoss)
		{
			return LfStringField.FromString(ListConverters[listCode].LfItemKeyString(fdoPoss, _wsEn));
		}

		public LfStringArrayField ToStringArrayField(string listCode, IEnumerable<ICmPossibility> fdoPossCollection)
		{
			return LfStringArrayField.FromStrings(ListConverters[listCode].LfItemKeyStrings(fdoPossCollection, _wsEn));
		}

		// Special case: LF sense Status field is a StringArray, but FDO sense status is single possibility
		public LfStringArrayField ToStringArrayField(string listCode, ICmPossibility fdoPoss)
		{
			return LfStringArrayField.FromSingleString(ListConverters[listCode].LfItemKeyString(fdoPoss, _wsEn));
		}

		/// <summary>
		/// Convert FDO lex entry to LF lex entry.
		/// </summary>
		/// <returns>LF entry
		/// <param name="fdoEntry">Fdo entry.</param>
		/// <param name="lfCustomFieldList">Updated dictionary of custom field name and custom field settings.</param>
		public LfLexEntry FdoLexEntryToLfLexEntry(ILexEntry fdoEntry, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			if (fdoEntry == null) return null;

			ILgWritingSystem AnalysisWritingSystem = Cache.LanguageProject.DefaultAnalysisWritingSystem;
			ILgWritingSystem VernacularWritingSystem = Cache.LanguageProject.DefaultVernacularWritingSystem;

			var lfEntry = new LfLexEntry();

			IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
			if (fdoLexeme == null)
				lfEntry.Lexeme = null;
			else
				lfEntry.Lexeme = ToMultiText(fdoLexeme.Form);
			// Other fields of fdoLexeme (AllomorphEnvironments, LiftResidue, MorphTypeRA, etc.) not mapped

			// Fields below in alphabetical order by ILexSense property, except for Lexeme
			foreach (IMoForm allomorph in fdoEntry.AlternateFormsOS)
			{
				// Do nothing; LanguageForge doesn't currently handle allomorphs, so we don't convert them
			}
			lfEntry.EntryBibliography = ToMultiText(fdoEntry.Bibliography);
			// TODO: Consider whether to use fdoEntry.CitationFormWithAffixType instead
			// (which would produce "-s" instead of "s" for the English plural suffix, for instance)
			lfEntry.CitationForm = ToMultiText(fdoEntry.CitationForm);
			lfEntry.Note = ToMultiText(fdoEntry.Comment);

			// DateModified and DateCreated can be confusing, because LF and FDO are doing two different
			// things with them. In FDO, there is just one DateModified and one DateCreated; simple. But
			// in LF, there is an AuthorInfo record as well, which contains its own ModifiedDate and CreatedDate
			// fields. (Note the word order: there's LfEntry.DateCreated, and LfEntry.AuthorInfo.CreatedDate).

			// The conversion we have chosen to use is: AuthorInfo will correspond to FDO. So FDO.DateCreated
			// becomes AuthorInfo.CreatedDate, and FDO.DateModified becomes AuthorInfo.ModifiedDate. The two
			// fields on the LF entry will instead refer to when the *Mongo record* was created or modified,
			// and the LfEntry.DateCreated and LfEntry.DateModified fields will never be put into FDO.

			// LanguageForge needs this modified to know there is changed data
			lfEntry.DateModified = fdoEntry.DateModified.ToUniversalTime();
			if (LfProject.IsInitialClone)
			{
				var now = DateTime.UtcNow;
				lfEntry.DateCreated = now;
				lfEntry.DateModified = now;
			}

			if (lfEntry.AuthorInfo == null)
				lfEntry.AuthorInfo = new LfAuthorInfo();
			lfEntry.AuthorInfo.CreatedByUserRef = null;
			lfEntry.AuthorInfo.CreatedDate = fdoEntry.DateCreated.ToUniversalTime();
			lfEntry.AuthorInfo.ModifiedByUserRef = null;
			lfEntry.AuthorInfo.ModifiedDate = fdoEntry.DateModified.ToUniversalTime();

			ILexEtymology fdoEtymology = fdoEntry.EtymologyOA;
			if (fdoEtymology != null)
			{
				lfEntry.Etymology = ToMultiText(fdoEtymology.Form);
				lfEntry.EtymologyComment = ToMultiText(fdoEtymology.Comment);
				lfEntry.EtymologyGloss = ToMultiText(fdoEtymology.Gloss);
				lfEntry.EtymologySource = LfMultiText.FromSingleStringMapping(AnalysisWritingSystem.Id, fdoEtymology.Source);
				// fdoEtymology.LiftResidue not mapped
			}
			lfEntry.Guid = fdoEntry.Guid;
			lfEntry.LiftId = fdoEntry.LIFTid;
			lfEntry.LiteralMeaning = ToMultiText(fdoEntry.LiteralMeaning);
			if (fdoEntry.PrimaryMorphType != null) {
				lfEntry.MorphologyType = fdoEntry.PrimaryMorphType.NameHierarchyString;
			}
			// TODO: Once LF's data model is updated from a single pronunciation to an array of pronunciations, convert all of them instead of just the first. E.g.,
			//foreach (ILexPronunciation fdoPronunciation in fdoEntry.PronunciationsOS) { ... }
			if (fdoEntry.PronunciationsOS.Count > 0)
			{
				ILexPronunciation fdoPronunciation = fdoEntry.PronunciationsOS.First();
				lfEntry.Pronunciation = ToMultiText(fdoPronunciation.Form);
				lfEntry.CvPattern = LfMultiText.FromSingleITsString(fdoPronunciation.CVPattern, Cache.WritingSystemFactory);
				lfEntry.Tone = LfMultiText.FromSingleITsString(fdoPronunciation.Tone, Cache.WritingSystemFactory);
				// TODO: Map fdoPronunciation.MediaFilesOS properly (converting video to sound files if necessary)
				lfEntry.Location = ToStringField(LocationListCode, fdoPronunciation.LocationRA);
			}
			lfEntry.EntryRestrictions = ToMultiText(fdoEntry.Restrictions);
			if (lfEntry.Senses == null) // Shouldn't happen, but let's be careful
				lfEntry.Senses = new List<LfSense>();
			lfEntry.Senses.AddRange(fdoEntry.SensesOS.Select(s => FdoSenseToLfSense(s, lfCustomFieldList)));
			lfEntry.SummaryDefinition = ToMultiText(fdoEntry.SummaryDefinition);

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(fdoEntry, "entry", ListConverters, lfCustomFieldList);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfEntry.CustomFields = customFieldsBson;
			lfEntry.CustomFieldGuids = customFieldGuids;

			return lfEntry;

			/* Fields not mapped because it doesn't make sense to map them (e.g., Hvo, backreferences, etc):
			fdoEntry.ComplexFormEntries;
			fdoEntry.ComplexFormEntryRefs;
			fdoEntry.ComplexFormsNotSubentries;
			fdoEntry.EntryRefsOS;
			fdoEntry.HasMoreThanOneSense;
			fdoEntry.HeadWord; // Read-only virtual property
			fdoEntry.IsMorphTypesMixed; // Read-only property
			fdoEntry.LexEntryReferences;
			fdoEntry.MainEntriesOrSensesRS;
			fdoEntry.MinimalLexReferences;
			fdoEntry.MorphoSyntaxAnalysesOC;
			fdoEntry.MorphTypes;
			fdoEntry.NumberOfSensesForEntry;
			fdoEntry.PicturesOfSenses;

			*/

			/* Fields that would make sense to map, but that we don't because LF doesn't handle them (e.g., allomorphs):
			fdoEntry.AllAllomorphs; // LF doesn't handle allomorphs, so skip all allomorph-related fields
			fdoEntry.AlternateFormsOS;
			fdoEntry.CitationFormWithAffixType; // Citation form already mapped
			fdoEntry.DoNotPublishInRC;
			fdoEntry.DoNotShowMainEntryInRC;
			fdoEntry.DoNotUseForParsing;
			fdoEntry.HomographForm;
			fdoEntry.HomographFormKey;
			fdoEntry.HomographNumber;
			fdoEntry.ImportResidue;
			fdoEntry.LiftResidue;
			fdoEntry.PronunciationsOS
			fdoEntry.PublishAsMinorEntry;
			fdoEntry.PublishIn;
			fdoEntry.ShowMainEntryIn;
			fdoEntry.Subentries;
			fdoEntry.VariantEntryRefs;
			fdoEntry.VariantFormEntries;
			fdoEntry.VisibleComplexFormBackRefs;
			fdoEntry.VisibleComplexFormEntries;
			fdoEntry.VisibleVariantEntryRefs;

			*/
		}

		/// <summary>
		/// Convert FDO sense to LF sense.
		/// </summary>
		/// <returns>LF sense
		/// <param name="fdoSense">Fdo sense.</param>
		/// <param name="lfCustomFieldList">Updated dictionary of custom field name and custom field settings.</param>
		public LfSense FdoSenseToLfSense(ILexSense fdoSense, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			var lfSense = new LfSense();

			ILgWritingSystem VernacularWritingSystem = Cache.LanguageProject.DefaultVernacularWritingSystem;
			ILgWritingSystem AnalysisWritingSystem = Cache.LanguageProject.DefaultAnalysisWritingSystem;

			lfSense.Guid = fdoSense.Guid;
			lfSense.Gloss = ToMultiText(fdoSense.Gloss);
			lfSense.Definition = ToMultiText(fdoSense.Definition);

			// Fields below in alphabetical order by ILexSense property, except for Guid, Gloss and Definition
			lfSense.AcademicDomains = ToStringArrayField(AcademicDomainListCode, fdoSense.DomainTypesRC);
			lfSense.AnthropologyCategories = ToStringArrayField(AnthroCodeListCode, fdoSense.AnthroCodesRC);
			lfSense.AnthropologyNote = ToMultiText(fdoSense.AnthroNote);
			lfSense.DiscourseNote = ToMultiText(fdoSense.DiscourseNote);
			lfSense.EncyclopedicNote = ToMultiText(fdoSense.EncyclopedicInfo);
			if (fdoSense.ExamplesOS != null)
			{
				lfSense.Examples = new List<LfExample>(fdoSense.ExamplesOS.Select(e => FdoExampleToLfExample(e, lfCustomFieldList)));
			}

			lfSense.GeneralNote = ToMultiText(fdoSense.GeneralNote);
			lfSense.GrammarNote = ToMultiText(fdoSense.GrammarNote);
			lfSense.LiftId = fdoSense.LIFTid;
			if (fdoSense.MorphoSyntaxAnalysisRA != null)
			{
				IPartOfSpeech secondaryPos = null; // Only used in derivational affixes
				IPartOfSpeech pos = ConvertFdoToMongoPartsOfSpeech.FromMSA(fdoSense.MorphoSyntaxAnalysisRA, out secondaryPos);
				// Sometimes the part of speech can be null for legitimate reasons, so check the known class IDs before warning of an unknown MSA type
				if (pos == null && !ConvertFdoToMongoPartsOfSpeech.KnownMsaClassIds.Contains(fdoSense.MorphoSyntaxAnalysisRA.ClassID))
					Logger.Warning("Got MSA of unknown type {0} in sense {1} in project {2}",
						fdoSense.MorphoSyntaxAnalysisRA.GetType().Name,
						fdoSense.Guid,
						LfProject.ProjectCode);
				else
				{
					lfSense.PartOfSpeech = ToStringField(GrammarListCode, pos);
					lfSense.SecondaryPartOfSpeech = ToStringField(GrammarListCode, secondaryPos); // It's fine if secondaryPos is still null here
				}
			}
			lfSense.PhonologyNote = ToMultiText(fdoSense.PhonologyNote);
			if (fdoSense.PicturesOS != null)
			{
				lfSense.Pictures = new List<LfPicture>(fdoSense.PicturesOS.Select(FdoPictureToLfPicture));
				//Use the commented code for debugging into FdoPictureToLfPicture
				//
				//lfSense.Pictures = new List<LfPicture>();
				//foreach (var fdoPic in fdoSense.PicturesOS)
				//	lfSense.Pictures.Add(FdoPictureToLfPicture(fdoPic));
			}
			lfSense.SenseBibliography = ToMultiText(fdoSense.Bibliography);
			lfSense.SensePublishIn = ToStringArrayField(PublishInListCode, fdoSense.PublishIn);
			lfSense.SenseRestrictions = ToMultiText(fdoSense.Restrictions);

			if (fdoSense.ReversalEntriesRC != null)
			{
				IEnumerable<string> reversalEntries = fdoSense.ReversalEntriesRC.Select(fdoReversalEntry => fdoReversalEntry.LongName);
				lfSense.ReversalEntries = LfStringArrayField.FromStrings(reversalEntries);
			}
			lfSense.ScientificName = LfMultiText.FromSingleITsString(fdoSense.ScientificName, Cache.WritingSystemFactory);
			lfSense.SemanticDomain = ToStringArrayField(SemDomListCode, fdoSense.SemanticDomainsRC);
			lfSense.SemanticsNote = ToMultiText(fdoSense.SemanticsNote);
			// fdoSense.SensesOS; // Not mapped because LF doesn't handle subsenses. TODO: When LF handles subsenses, map this one.
			lfSense.SenseType = ToStringField(SenseTypeListCode, fdoSense.SenseTypeRA);
			lfSense.SociolinguisticsNote = ToMultiText(fdoSense.SocioLinguisticsNote);
			if (fdoSense.Source != null)
			{
				lfSense.Source = LfMultiText.FromSingleITsString(fdoSense.Source, Cache.WritingSystemFactory);
			}
			lfSense.Status = ToStringArrayField(StatusListCode, fdoSense.StatusRA);
			lfSense.Usages = ToStringArrayField(UsageTypeListCode, fdoSense.UsageTypesRC);


			/* Fields not mapped because it doesn't make sense to map them (e.g., Hvo, backreferences, etc):
			fdoSense.AllOwnedObjects;
			fdoSense.AllSenses;
			fdoSense.Cache;
			fdoSense.CanDelete;
			fdoSense.ChooserNameTS;
			fdoSense.ClassID;
			fdoSense.ClassName;
			fdoSense.Entry;
			fdoSense.EntryID;
			fdoSense.FullReferenceName;
			fdoSense.GetDesiredMsaType();
			fdoSense.Hvo;
			fdoSense.ImportResidue;
			fdoSense.IndexInOwner;
			fdoSense.IsValidObject;
			fdoSense.LexSenseReferences;
			fdoSense.LongNameTSS;
			fdoSense.ObjectIdName;
			fdoSense.OwnedObjects;
			fdoSense.Owner;
			fdoSense.OwningFlid;
			fdoSense.OwnOrd;
			fdoSense.ReferringObjects;
			fdoSense.ReversalNameForWs(wsVern);
			fdoSense.SandboxMSA; // Set-only property
			fdoSense.Self;
			fdoSense.Services;
			fdoSense.ShortName;
			fdoSense.ShortNameTSS;
			fdoSense.SortKey;
			fdoSense.SortKey2;
			fdoSense.SortKey2Alpha;
			fdoSense.SortKeyWs;
			fdoSense.VariantFormEntryBackRefs;
			fdoSense.VisibleComplexFormBackRefs;
			*/

			/* Fields not mapped because LanguageForge doesn't handle that data:
			fdoSense.AppendixesRC;
			fdoSense.ComplexFormEntries;
			fdoSense.ComplexFormsNotSubentries;
			fdoSense.DoNotPublishInRC;
			fdoSense.Subentries;
			fdoSense.ThesaurusItemsRC;
			fdoSense.LiftResidue;
			fdoSense.LexSenseOutline;
			*/

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(fdoSense, "senses", ListConverters, lfCustomFieldList);
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
		/// Convert FDO example LF example.
		/// </summary>
		/// <returns>LF example
		/// <param name="fdoExample">Fdo example.</param>
		/// <param name="lfCustomFieldList">Updated dictionary of custom field name and custom field settings.</param>
		public LfExample FdoExampleToLfExample(ILexExampleSentence fdoExample, Dictionary<string, LfConfigFieldBase> lfCustomFieldList)
		{
			var lfExample = new LfExample();

			ILgWritingSystem AnalysisWritingSystem = Cache.LanguageProject.DefaultAnalysisWritingSystem;
			ILgWritingSystem VernacularWritingSystem = Cache.LanguageProject.DefaultVernacularWritingSystem;

			lfExample.Guid = fdoExample.Guid;
			lfExample.ExamplePublishIn = ToStringArrayField(PublishInListCode, fdoExample.PublishIn);
			lfExample.Sentence = ToMultiText(fdoExample.Example);
			lfExample.Reference = LfMultiText.FromSingleITsString(fdoExample.Reference, Cache.WritingSystemFactory);
			// ILexExampleSentence fields we currently do not convert:
			// fdoExample.DoNotPublishInRC;
			// fdoExample.LiftResidue;

			// NOTE: Currently, LanguageForge only stores one translation per example, whereas FDO can store
			// multiple translations with (possibly) different statuses (as freeform strings, like "old", "updated",
			// "needs fixing"...). Until LanguageForge acquires a data model where translations are stored in a list,
			// we will save only the first translation (if any) to Mongo. We also save the GUID so that the Mongo->FDO
			// direction will know which ICmTranslation object to update with any changes.
			// TODO: Once LF improves its data model for translations, persist all of them instead of just the first.
			foreach (ICmTranslation translation in fdoExample.TranslationsOC.Take(1))
			{
				lfExample.Translation = ToMultiText(translation.Translation);
				lfExample.TranslationGuid = translation.Guid;
			}

			BsonDocument customFieldsAndGuids = _convertCustomField.GetCustomFieldsForThisCmObject(fdoExample, "examples", ListConverters, lfCustomFieldList);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfExample.CustomFields = customFieldsBson;
			lfExample.CustomFieldGuids = customFieldGuids;
			return lfExample;
		}

		public LfPicture FdoPictureToLfPicture(ICmPicture fdoPicture)
		{
			var result = new LfPicture();
			result.Caption = ToMultiText(fdoPicture.Caption);
			if ((fdoPicture.PictureFileRA != null) && (!string.IsNullOrEmpty(fdoPicture.PictureFileRA.InternalPath)))
			{
				result.FileName = FdoPictureFilenameToLfPictureFilename(fdoPicture.PictureFileRA.InternalPath);
			}
			result.Guid = fdoPicture.Guid;
			// Unmapped ICmPicture fields include:
			// fdoPicture.Description;
			// fdoPicture.LayoutPos;
			// fdoPicture.LocationMax;
			// fdoPicture.LocationMin;
			// fdoPicture.LocationRangeType;
			// fdoPicture.ScaleFactor;
			return result;
		}

		public static string FdoPictureFilenameToLfPictureFilename(string fdoInternalFilename)
		{
			// Remove "Pictures" directory from internal path name
			// If the incoming internal path doesn't begin with "Pictures", then preserve the full external path.
			return Regex.Replace(fdoInternalFilename, @"^Pictures[/\\]", "");
		}

		/// <summary>
		/// Converts FDO writing systems to LF input systems
		/// </summary>
		/// <returns>The list of LF input systems.</returns>
		private Dictionary<string, LfInputSystemRecord> FdoWsToLfWs()
		{
			// Using var here so that we'll stay compatible with both FW 8 and 9 (the type of these two lists changed between 8 and 9).
			var vernacularWSList = Cache.LanguageProject.CurrentVernacularWritingSystems;
			var analysisWSList = Cache.LanguageProject.CurrentAnalysisWritingSystems;

			var lfWsList = new Dictionary<string, LfInputSystemRecord>();
			foreach (var fdoWs in Cache.LanguageProject.AllWritingSystems)
			{
				var lfWs = new LfInputSystemRecord()
				{
					//These are for current libpalaso with SIL Writing Systems.
					// TODO: handle legacy WS definition
					Abbreviation = fdoWs.Abbreviation,
					IsRightToLeft = fdoWs.RightToLeftScript,
					LanguageName = fdoWs.LanguageName,
					#if FW8_COMPAT
					Tag = fdoWs.Id,
					#else
					Tag = fdoWs.LanguageTag,
					#endif
					VernacularWS = vernacularWSList.Contains(fdoWs),
					AnalysisWS = analysisWSList.Contains(fdoWs)
				};

				#if FW8_COMPAT
				lfWsList.Add(fdoWs.Id, lfWs);
				#else
				lfWsList.Add(fdoWs.LanguageTag, lfWs);
				#endif
			}
			return lfWsList;
		}

		public ConvertFdoToMongoOptionList ConvertOptionListFromFdo(ILfProject project, string listCode, ICmPossibilityList fdoOptionList, bool updateMongoList = true)
		{
			LfOptionList lfExistingOptionList = Connection.GetLfOptionListByCode(project, listCode);
			var converter = new ConvertFdoToMongoOptionList(lfExistingOptionList, _wsEn, listCode, Logger, Cache.WritingSystemFactory);
			LfOptionList lfChangedOptionList = converter.PrepareOptionListUpdate(fdoOptionList);
			if (updateMongoList)
				Connection.UpdateRecord(project, lfChangedOptionList, listCode);
			return new ConvertFdoToMongoOptionList(lfChangedOptionList, _wsEn, listCode, Logger, Cache.WritingSystemFactory);
		}
	}
}

