// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LfMerge.Core.DataConverters.CanonicalSources;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using LfMergeBridge.LfMergeModel;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.Reporting;
using LfMerge.Core.Settings;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Progress;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToLcmLexicon
	{
		private LfMergeSettings Settings { get; set; }
		private ILfProject LfProject { get; set; }
		private FwProject FwProject { get; set; }
		private LcmCache Cache { get; set; }
		private IProgress Progress { get; set; }
		private FwServiceLocatorCache ServiceLocator { get; set; }
		private ILogger Logger { get; set; }
		private IMongoConnection Connection { get; set; }
		private MongoProjectRecord ProjectRecord { get; set; }
		private EntryCounts EntryCounts { get; set; }

		//private IEnumerable<ILgWritingSystem> _analysisWritingSystems;
		//private IEnumerable<ILgWritingSystem> _vernacularWritingSystems;

		private int _wsEn;
		private ConvertMongoToLcmCustomField _convertCustomField;

		// Entries where LF's copy differs from FieldWorks but no LF user has touched it since the
		// last sync, so FieldWorks holds the newer version (see EditedInLfSinceLastSync)
		private int _changedOnlyInFieldWorks;
		private int _absentOnlyFromFieldWorks;

		// Shorter names to use in this class since MagicStrings.LfOptionListCodeForGrammaticalInfo
		// (etc.) are real mouthfuls
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

		private IDictionary<string, ConvertMongoToLcmOptionList> ListConverters;

		private ICmPossibility _freeTranslationType; // Used in LfExampleToLcmExample(), but cached here

		public ConvertMongoToLcmLexicon(LfMergeSettings settings, ILfProject lfproject, ILogger logger, IProgress progress,
			IMongoConnection connection, MongoProjectRecord projectRecord, EntryCounts entryCounts)
		{
			EntryCounts = entryCounts;
			Settings = settings;
			LfProject = lfproject;
			Logger = logger;
			Progress = progress;
			Connection = connection;
			ProjectRecord = projectRecord;

			FwProject = LfProject.FieldWorksProject;
			Cache = FwProject.Cache;
			ServiceLocator = FwProject.ServiceLocator;
			// These writing system search orders will be used in BestStringAndWsFromMultiText and related functions
			//_analysisWritingSystems = ServiceLocator.LanguageProject.CurrentAnalysisWritingSystems;
			//_vernacularWritingSystems = ServiceLocator.LanguageProject.CurrentVernacularWritingSystems;

			ListConverters = new Dictionary<string, ConvertMongoToLcmOptionList>();
			ListConverters[GrammarListCode] = PrepareOptionListConverter(GrammarListCode);
			ListConverters[SemDomListCode] = PrepareOptionListConverter(SemDomListCode);
			ListConverters[AcademicDomainListCode] = PrepareOptionListConverter(AcademicDomainListCode);
			ListConverters[LocationListCode] = PrepareOptionListConverter(LocationListCode);
			ListConverters[UsageTypeListCode] = PrepareOptionListConverter(UsageTypeListCode);
			ListConverters[SenseTypeListCode] = PrepareOptionListConverter(SenseTypeListCode);
			ListConverters[AnthroCodeListCode] = PrepareOptionListConverter(AnthroCodeListCode);
			ListConverters[StatusListCode] = PrepareOptionListConverter(StatusListCode);

			_wsEn = ServiceLocator.WritingSystemFactory.GetWsFromStr("en");

			// Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), replace the code block
			// above with this one (that provides TWO parameters to PrepareOptionListConverter)
			#if false
			ListConverters[GrammarListCode] = PrepareOptionListConverter(GrammarListCode, ServiceLocator.LanguageProject.PartsOfSpeechOA);
			ListConverters[SemDomListCode] = PrepareOptionListConverter(SemDomListCode, ServiceLocator.LanguageProject.SemanticDomainListOA);
			ListConverters[AcademicDomainListCode] = PrepareOptionListConverter(AcademicDomainListCode, ServiceLocator.LanguageProject.LexDbOA.DomainTypesOA);
			ListConverters[LocationListCode] = PrepareOptionListConverter(LocationListCode, ServiceLocator.LanguageProject.LocationsOA);
			ListConverters[UsageTypeListCode] = PrepareOptionListConverter(UsageTypeListCode, ServiceLocator.LanguageProject.LexDbOA.UsageTypesOA);
			ListConverters[SenseTypeListCode] = PrepareOptionListConverter(SenseTypeListCode, ServiceLocator.LanguageProject.LexDbOA.SenseTypesOA);
			ListConverters[AnthroCodeListCode] = PrepareOptionListConverter(AnthroCodeListCode, ServiceLocator.LanguageProject.AnthroListOA);
			ListConverters[StatusListCode] = PrepareOptionListConverter(StatusListCode, ServiceLocator.LanguageProject.StatusOA);
			#endif

			if (ServiceLocator.LanguageProject != null && ServiceLocator.LanguageProject.TranslationTagsOA != null)
			{
				_freeTranslationType = ServiceLocator.ObjectRepository.GetObject(LangProjectTags.kguidTranFreeTranslation)
					as ICmPossibility;
				if (_freeTranslationType == null) // Shouldn't happen, but let's have a fallback possibility
					_freeTranslationType = ServiceLocator.LanguageProject.TranslationTagsOA.PossibilitiesOS.FirstOrDefault();
			}
		}

		private ConvertMongoToLcmOptionList PrepareOptionListConverter(string listCode)
		{
			LfOptionList optionListToConvert = Connection.GetLfOptionListByCode(LfProject, listCode);
			return new ConvertMongoToLcmOptionList(GetInstance<ICmPossibilityRepository>(),
				optionListToConvert, Logger, CanonicalOptionListSource.Create(listCode));
		}

		// Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), replace the function
		// above (that takes ONE parameter) with this one (that takes TWO parameters)
		#if false
		public ConvertMongoToLcmOptionList PrepareOptionListConverter(string listCode, ICmPossibilityList parentList)
		{
			LfOptionList optionListToConvert = Connection.GetLfOptionListByCode(LfProject, listCode);
			return new ConvertMongoToLcmOptionList(GetInstance<ICmPossibilityRepository>(), optionListToConvert, Logger, parentList, _wsEn, CanonicalOptionListSource.Create(listCode));
		}
		#endif

		public ConversionError<LfLexEntry> RunConversion()
		{
			var exceptions = new ConversionError<LfLexEntry>();
			Logger.Notice("MongoToLcm: Converting lexicon for project {0}", LfProject.ProjectCode);
			// Logger.Debug("Running \"fake\" MtFComments, should see comments show up below:");
			var entryObjectIdToGuidMappings = Connection.GetGuidsByObjectIdForCollection(LfProject, MagicStrings.LfCollectionNameForLexicon);
			EntryCounts.Reset();
			// Counting the lexicon before any writing system is created, because which list a new
			// writing system belongs in depends on what it is actually used for. This is a second
			// streaming pass over Mongo; GetLexicon yields from a cursor, so it does not all sit in
			// memory at once.
			LfWritingSystemUsage wsUsage = LfWritingSystemUsage.FromLexicon(GetLexicon(LfProject));

			// Update writing systems from project config input systems.  Won't commit till the end
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", Cache.ActionHandlerAccessor, () =>
				LfWsToLcmWs(ProjectRecord.InputSystems, wsUsage));

			// Set English ws handle again in case it changed
			_wsEn = ServiceLocator.WritingSystemFactory.GetWsFromStr("en");

			_convertCustomField = new ConvertMongoToLcmCustomField(Cache, ServiceLocator, Logger, _wsEn);

			IEnumerable<LfLexEntry> lexicon = GetLexicon(LfProject);
			_changedOnlyInFieldWorks = 0;
			_absentOnlyFromFieldWorks = 0;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", Cache.ActionHandlerAccessor, () =>
				{
					#if false  // Once we allow LanguageForge to create optionlist items with "canonical" values (parts of speech, semantic domains, etc.), uncomment this block
					foreach (ConvertMongoToLcmOptionList converter in ListConverters.Values)
					{
						converter.UpdateLcmOptionListFromLf(ProjectRecord.InterfaceLanguageCode);
					}
					#endif

					foreach (LfLexEntry lfEntry in lexicon)
					{
						try
						{
							LfLexEntryToLcmLexEntry(lfEntry);
						}
						catch (Exception e)
						{
							exceptions.AddEntryError(lfEntry, e);
						}
					}
				});
			if (_changedOnlyInFieldWorks > 0 || _absentOnlyFromFieldWorks > 0)
			{
				// Normally zero: LfMerge's working copy is what LF last synced with, so the two copies
				// can only differ where LF users have edited since. Anything else means the working copy
				// has moved on without LF, as a fresh clone of a project FLEx has kept working on has.
				Logger.Warning("MongoToLcm: {0} entries differ from FieldWorks and {1} are missing from it, though no LF user has " +
					"edited them since the last sync ({2}); kept FieldWorks's version of all of them",
					_changedOnlyInFieldWorks, _absentOnlyFromFieldWorks,
					LastSyncedDate == null ? "never synced" : LastSyncedDate.Value.ToString("u"));
			}
			// Comment conversion gets run AFTER lexicon conversion, so that any comments on new entries are handled correctly in FW
			var commCvtr = new ConvertMongoToLcmComments(Connection, LfProject, exceptions, Logger, Progress);
			var commErrors = commCvtr.RunConversion(entryObjectIdToGuidMappings);
			exceptions.AddCommentErrors(commErrors);
			if (Settings.CommitWhenDone)
				Cache.ActionHandlerAccessor.Commit();
			return exceptions;
		}

		// Shorthand for getting an instance from the cache's service locator
		private T GetInstance<T>() where T : class
		{
			return ServiceLocator.GetInstance<T>();
		}

		private IEnumerable<LfLexEntry> GetLexicon(ILfProject project)
		{
			return Connection.GetRecords<LfLexEntry>(project, MagicStrings.LfCollectionNameForLexicon);
		}

		// The field groups whose vernacular/analysis role is never in doubt, because FieldWorks fixes
		// it: a headword and an example sentence are vernacular, a gloss and a translation are not.
		// Everything else is classified per project by which of these its writing systems appear in
		// -- a fixed list cannot be right for every project: in the 2026-07-06 corpus 741 projects
		// configure etymology with analysis writing systems and 378 with vernacular ones.
		//
		// Counting that corpus, for each field, the projects whose text there is exclusively
		// vernacular against exclusively analysis: the example sentence is 332 to 5, the example
		// translation 1 to 320, the entry note 0 to 255 and the literal meaning 4 to 136.
		//
		// The pronunciation field is deliberately NOT here. It looks vernacular at 114 to 16, but
		// that is 12% analysis, above the MinorityShare a single writing system would have to stay
		// under to count as one role alone, so projects evidently use it for more than one thing.
		// Its text still counts for something in ClassifyByText, though: see there.
		private static readonly string[] VernacularAnchorFields = {
			LfWritingSystemUsage.Lexeme,
			LfWritingSystemUsage.CitationForm,
			LfWritingSystemUsage.ExampleSentence,
		};
		private static readonly string[] AnalysisAnchorFields = {
			LfWritingSystemUsage.Definition,
			LfWritingSystemUsage.Gloss,
			LfWritingSystemUsage.ExampleTranslation,
			LfWritingSystemUsage.Note,
			LfWritingSystemUsage.LiteralMeaning,
		};

		/// <summary>
		/// Work out which writing systems this project treats as vernacular and which as analysis,
		/// from its own config.
		///
		/// The lexeme and citation form fields are vernacular by definition, the sense definition and
		/// gloss are analysis by definition. Every other field carrying input systems (etymology, the
		/// example sentence, custom fields) is assigned by overlap: a writing system that appears in
		/// the vernacular anchors and NOT in the analysis anchors is evidence the field is vernacular,
		/// and vice versa. A writing system present in both anchors -- "en" very often is -- is no
		/// evidence either way and is ignored for that purpose; it is both vernacular and analysis.
		///
		/// Evidence decides the role of the field, not of every writing system in it, and it has to
		/// point one way: a field offering writing systems exclusive to each role, such as an
		/// etymology configured as [seh, pt], is evidence of neither. So it does not make Portuguese
		/// vernacular, nor anything else it offers that the anchors have not placed.
		///
		/// Where the lexicon has text to show for a writing system, that text decides instead: a
		/// writing system holding nearly all of its text in the vernacular anchors is vernacular
		/// whatever the config offers, and vice versa. See <see cref="MinorityShare"/>.
		///
		/// This replaces comparing each tag against ProjectRecord.LanguageCode, which named exactly one
		/// vernacular writing system and got it wrong whenever languageCode disagreed with the lexeme
		/// field: flh-flex has languageCode "flh-x-ortho" (used only by etymology) while its lexeme
		/// uses "flh-x-cm", and odo has languageCode "th" which is not among its writing systems at all,
		/// leaving its 24,460 "en" headwords classified as analysis.
		/// </summary>
		/// <param name="usage">
		/// How much text each writing system holds in each field, or null to go on the config alone.
		/// </param>
		/// <returns>
		/// The vernacular tags; the analysis tags; and the tags that could not be resolved. The first two sets overlap wherever a writing system plays both roles. A tag in
		/// neither set -- one no config field uses -- is analysis, as it always has been.
		///
		/// Unresolved writing systems are treated as BOTH vernacular and analysis, since nothing says
		/// which they are and a writing system missing from the list a field draws on leaves that
		/// field's data with nowhere to go. The FieldWorks fields such a writing system most often
		/// feeds (etymology form, example sentence) are vernacular-typed, which is why being only
		/// analysis will not do; but a custom field can be analysis-typed, as a French-only notes
		/// field would be, which is why being only vernacular will not do either. 11 projects in the
		/// corpus need this, e.g. grc-vie-flex, whose etymologies are in Hebrew and Aramaic --
		/// neither its vernacular (Greek) nor its analysis (English, Vietnamese).
		/// </returns>
		public static (ISet<string> Vernacular, ISet<string> Analysis, ISet<string> Unresolved)
			ClassifyVernacularWritingSystems(LfProjectConfig config, string languageCode,
				LfWritingSystemUsage usage = null)
		{
			var byPath = new Dictionary<string, ISet<string>>();
			CollectInputSystems((config == null) ? null : config.Entry, "", byPath);

			var vernacular = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var analysis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Writing systems whose text has already answered for them. They keep that answer: the
			// config below may offer them for fields nobody writes in, and must not overrule it.
			var decidedByText = ClassifyByText(usage, byPath, vernacular, analysis);

			var vernacularAnchor = Union(byPath, VernacularAnchorFields);
			var analysisAnchor = Union(byPath, AnalysisAnchorFields);
			vernacular.UnionWith(vernacularAnchor.Where(tag => !decidedByText.Contains(tag)));
			analysis.UnionWith(analysisAnchor.Where(tag => !decidedByText.Contains(tag)));

			// Only a writing system exclusive to one role is evidence of a field's role.
			var vernacularOnly = new HashSet<string>(vernacular, StringComparer.OrdinalIgnoreCase);
			vernacularOnly.ExceptWith(analysis);
			var analysisOnly = new HashSet<string>(analysis, StringComparer.OrdinalIgnoreCase);
			analysisOnly.ExceptWith(vernacular);

			var anchors = new HashSet<string>(VernacularAnchorFields.Concat(AnalysisAnchorFields));
			foreach (var field in byPath)
			{
				if (anchors.Contains(field.Key) || field.Value.Count == 0)
					continue;
				// A writing system its own text has spoken for is still evidence of what this field
				// is for, but nothing here may change the answer it was given.
				var undecided = field.Value.Where(tag => !decidedByText.Contains(tag)).ToList();
				if (undecided.Count == 0)
					continue;
				bool withVernacular = field.Value.Any(vernacularOnly.Contains);
				bool withAnalysis = field.Value.Any(analysisOnly.Contains);
				if (withVernacular && !withAnalysis)
				{
					vernacular.UnionWith(undecided);
				}
				else if (withAnalysis && !withVernacular)
				{
					analysis.UnionWith(undecided);
				}
				else
				{
					// Overlaps neither role, or both: nothing says which role this field plays. A field
					// offering a vernacular-only and an analysis-only writing system side by side is
					// no more evidence for one role than the other, just as only unanimous company
					// counts in ResolveByCompanions.
					//
					// That puts in doubt only the writing systems nothing else has placed. One already
					// placed keeps its place: "en" in both the lexeme and the gloss is not made
					// doubtful by also turning up in a note, and if it were, the passes below could
					// strip one of its roles on the strength of the company it keeps there.
					var unplaced = undecided
						.Where(tag => !vernacular.Contains(tag) && !analysis.Contains(tag)).ToList();
					vernacular.UnionWith(unplaced);
					analysis.UnionWith(unplaced);
					unresolved.UnionWith(unplaced);
				}
			}

			ResolveByCompanions(byPath, vernacular, analysis, unresolved);
			ResolveByLanguageAffinity(vernacular, analysis, unresolved);

			// Safety net for a config LfMerge cannot read: without this such a project would get no
			// vernacular writing system at all, which is worse than the old rule.
			if (vernacular.Count == 0 && !string.IsNullOrEmpty(languageCode))
				vernacular.Add(languageCode);

			return (vernacular, analysis, unresolved);
		}

		/// <summary>
		/// Settles what is left by the company a writing system keeps. A writing system nothing has
		/// settled still shares its fields with others, and those may all have been settled: a
		/// custom field holding the vernacular and one unplaced writing system is being used for
		/// vernacular content, so the unplaced one is vernacular too.
		///
		/// Only unanimous company counts. A field holding writing systems of both roles says
		/// nothing, which is the same reason a writing system in both anchors is no evidence of a
		/// field's role.
		/// </summary>
		private static void ResolveByCompanions(IDictionary<string, ISet<string>> byPath,
			ISet<string> vernacular, ISet<string> analysis, ISet<string> unresolved)
		{
			if (unresolved.Count == 0)
				return;

			// Unresolved writing systems sit in both sets, so they are in neither of these and
			// cannot vote for each other.
			var vernacularOnly = new HashSet<string>(vernacular, StringComparer.OrdinalIgnoreCase);
			vernacularOnly.ExceptWith(analysis);
			var analysisOnly = new HashSet<string>(analysis, StringComparer.OrdinalIgnoreCase);
			analysisOnly.ExceptWith(vernacular);

			foreach (string tag in unresolved.ToList())
			{
				var companions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var field in byPath)
				{
					if (field.Value.Contains(tag))
						companions.UnionWith(field.Value);
				}
				companions.Remove(tag);
				bool withVernacular = companions.Any(vernacularOnly.Contains);
				bool withAnalysis = companions.Any(analysisOnly.Contains);
				if (withVernacular == withAnalysis)
					continue; // No company, or company of both roles: still nothing to go on.
				if (withVernacular)
					analysis.Remove(tag);
				else
					vernacular.Remove(tag);
				unresolved.Remove(tag);
			}
		}

		/// <summary>
		/// Settles what is left by language. A writing system plays the same role as the others
		/// that spell the same language: a phonetic or audio spelling of the vernacular is
		/// vernacular too, and a regional spelling of the analysis language is analysis.
		/// </summary>
		private static void ResolveByLanguageAffinity(ISet<string> vernacular, ISet<string> analysis,
			ISet<string> unresolved)
		{
			if (unresolved.Count == 0)
				return;

			// Unresolved writing systems sit in both sets, so they are in neither of these and
			// cannot vote for each other.
			var vernacularLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var analysisLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string tag in vernacular)
			{
				if (!analysis.Contains(tag))
					vernacularLanguages.Add(LanguageOf(tag));
			}
			foreach (string tag in analysis)
			{
				if (!vernacular.Contains(tag))
					analysisLanguages.Add(LanguageOf(tag));
			}

			foreach (string tag in unresolved.ToList())
			{
				string language = LanguageOf(tag);
				bool withVernacular = vernacularLanguages.Contains(language);
				bool withAnalysis = analysisLanguages.Contains(language);
				if (withVernacular == withAnalysis)
					continue; // The language is spoken for by both roles, or by neither.
				if (withVernacular)
					analysis.Remove(tag);
				else
					vernacular.Remove(tag);
				unresolved.Remove(tag);
			}
		}

		// Subtags that mark a variant of a language rather than a language of its own. FieldWorks
		// also appends "dupl1", "dupl2" and so on when a project needs a second writing system for
		// the same thing.
		private static readonly HashSet<string> VariantMarkers = new HashSet<string>(
			new[] { "fonipa", "etic", "emic", "audio" }, StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// The language a tag names, as far as telling one project's writing systems apart goes:
		/// the language subtag plus any private-use subtags that name the language rather than a
		/// variant of it. Script and region are left out, so "en" and "en-GB" are one language.
		///
		/// The private-use subtags have to stay, because under "qaa" -- the code for a language
		/// with no code of its own, which Language Forge projects lean on heavily -- they ARE the
		/// language: "qaa-x-kal" and "qaa-x-hbo" are no more the same language than "fr" and "de".
		/// So "qaa-x-kal", "qaa-fonipa-x-kal" and "qaa-Zxxx-x-kal-audio" all come out "qaa-kal",
		/// while "seh" and "seh-fonipa-x-etic" both come out "seh". The same goes for a tag that is
		/// private-use from the start: "x-kal" comes out "x-kal", not "x".
		/// </summary>
		private static string LanguageOf(string tag)
		{
			if (string.IsNullOrEmpty(tag))
				return tag;
			string[] parts = tag.Split('-');
			var language = new List<string> { parts[0] };
			int privateUse = Array.FindIndex(parts,
				part => part.Equals("x", StringComparison.OrdinalIgnoreCase));
			if (privateUse >= 0)
			{
				for (int i = privateUse + 1; i < parts.Length; i++)
				{
					if (VariantMarkers.Contains(parts[i]) ||
						parts[i].StartsWith("dupl", StringComparison.OrdinalIgnoreCase))
						continue;
					language.Add(parts[i]);
				}
			}
			return string.Join("-", language);
		}

		/// <summary>
		/// A writing system holding at most this share of its text in the fields of one role is
		/// taken to belong to the other role alone; in between, it plays both.
		///
		/// A few strays are not evidence of a second role. In brb-flex-2022 the phonetic writing
		/// system has 4,030 headwords and one stray string in an example reference, and FieldWorks
		/// has it as vernacular only; in cmo-khm-flex the vernacular has 7,815 vernacular strings
		/// against 300 in glosses, and is likewise vernacular only. The one writing system the four
		/// FieldWorks projects on hand really do list as BOTH sits at 28%, so anything between 4%
		/// and 28% separates the two, and 10% is the round number in the middle.
		/// </summary>
		public const double MinorityShare = 0.10;

		/// <summary>
		/// Classifies every writing system the lexicon has text for, by where that text sits.
		/// Adds them to <paramref name="vernacular"/> and <paramref name="analysis"/>, and returns
		/// the ones it answered for.
		///
		/// A writing system whose only text is in the pronunciation field is made vernacular,
		/// because FieldWorks draws its pronunciation writing systems from the vernacular ones
		/// (InitializePronunciationWritingSystems considers no others). That is not an answer, only
		/// a role it must have, so the config can still add analysis: the field is not an anchor
		/// (see VernacularAnchorFields), and a stray pronunciation string should not take a
		/// writing system's analysis role away. In the 2026-10-01 corpus this gives xin-flex's
		/// "xin", which holds 226 pronunciations and nothing else, the vernacular role it needs.
		/// </summary>
		private static ISet<string> ClassifyByText(LfWritingSystemUsage usage,
			IDictionary<string, ISet<string>> byPath, ISet<string> vernacular, ISet<string> analysis)
		{
			var decided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (usage == null || usage.EntriesCounted == 0)
				return decided;

			// Every writing system the config mentions, plus any the config forgot but the lexicon
			// uses anyway -- those need classifying just as much.
			var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (ISet<string> tags in byPath.Values)
				candidates.UnionWith(tags);
			foreach (string path in VernacularAnchorFields.Concat(AnalysisAnchorFields))
				candidates.UnionWith(usage.TagsWithText(path));
			candidates.UnionWith(usage.TagsWithText(LfWritingSystemUsage.Pronunciation));

			foreach (string tag in candidates)
			{
				int vernacularText = usage.NonEmptyCount(VernacularAnchorFields, tag);
				int analysisText = usage.NonEmptyCount(AnalysisAnchorFields, tag);
				int total = vernacularText + analysisText;
				if (total == 0)
				{
					// No text in a field of known role, so its text decides nothing.
					if (usage.NonEmptyCount(LfWritingSystemUsage.Pronunciation, tag) > 0)
						vernacular.Add(tag);
					continue;
				}
				double analysisShare = (double)analysisText / total;
				if (analysisShare <= MinorityShare)
					vernacular.Add(tag);
				else if (analysisShare >= 1.0 - MinorityShare)
					analysis.Add(tag);
				else
				{
					vernacular.Add(tag);
					analysis.Add(tag);
				}
				decided.Add(tag);
			}
			return decided;
		}

		private static ISet<string> Union(IDictionary<string, ISet<string>> byPath, IEnumerable<string> paths)
		{
			var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in paths)
			{
				ISet<string> tags;
				if (byPath.TryGetValue(path, out tags))
					result.UnionWith(tags);
			}
			return result;
		}

		/// <summary>
		/// Record every config field's input systems, keyed by its dotted path as Mongo spells it, so
		/// nesting goes through "fields" ("senses.fields.examples.fields.sentence").
		/// </summary>
		private static void CollectInputSystems(LfConfigFieldList fieldList, string path,
			IDictionary<string, ISet<string>> byPath)
		{
			if (fieldList == null || fieldList.Fields == null)
				return;
			foreach (var child in fieldList.Fields)
			{
				string childPath = string.IsNullOrEmpty(path) ? child.Key : path + "." + child.Key;
				var multiText = child.Value as LfConfigMultiText;
				if (multiText != null && multiText.InputSystems != null)
					byPath[childPath] = new HashSet<string>(multiText.InputSystems, StringComparer.OrdinalIgnoreCase);
				var nested = child.Value as LfConfigFieldList;
				if (nested != null)
					CollectInputSystems(nested, childPath + ".fields", byPath);
			}
		}

		/// <summary>
		/// Converts the list of LF input systems and adds them to Lcm writing systems
		/// </summary>
		/// <param name="lfWsList">List of LF input systems.</param>
		private void LfWsToLcmWs(Dictionary<string, LfInputSystemRecord> lfWsList,
			LfWritingSystemUsage usage = null)
		{
			// Between FW 8.2 and 9, a few classes and interfaces were renamed. The ones most relevant here are
			// IWritingSystemManager (interface was removed and replaced with the WritingSystemManager concrete class),
			// and PalasoWritingSystem which was replaced with CoreWritingSystemDefinition. Since their internals
			// didn't change much (and the only changes were in areas we don't access), we can use a simple compiler
			// define to choose the type of the wsm variable here (and the ws variable later) and we're fine.
			// HOWEVER, if the code inside #if...#endif blocks starts to grow, this is not an ideal solution. A better
			// solution if the code grows complex will be to write several classes to the same interface, each of which
			// can deal with one particular version of FW or Lcm. Register them all with Autofac with a way to choose among them
			// (http://docs.autofac.org/en/stable/register/registration.html#selection-of-an-implementation-by-parameter-value)
			// and then, at runtime, we can instantiate the particular class that's needed for dealing with *this* FW project.
			//
			// But for now, these #if...#endif blocks are enough. - 2016-03 RM
#if FW8_COMPAT
			// Note that we can't use ILgWritingSystemFactory here, because it doesn't have some methods we need later on.
			IWritingSystemManager wsManager = ServiceLocator.WritingSystemManager;
#else
			WritingSystemManager wsManager = ServiceLocator.WritingSystemManager;
#endif
			if (wsManager == null)
			{
				Logger.Error("Failed to find the writing system manager");
				return;
			}

			// Which writing systems are vernacular is derived from the project's own config rather
			// than from languageCode alone; see ClassifyVernacularWritingSystems.
			var classification = ClassifyVernacularWritingSystems(ProjectRecord.Config,
				ProjectRecord.LanguageCode, usage);
			ISet<string> vernacularTags = classification.Vernacular;
			ISet<string> analysisTags = classification.Analysis;
			if (classification.Unresolved.Count > 0)
			{
				Logger.Notice("MongoToLcm: writing system(s) {0} appear only in config fields whose "
					+ "vernacular/analysis role could not be determined; treating them as both",
					string.Join(", ", classification.Unresolved));
			}
			// TODO: Split the inside of this foreach() out into its own function
			foreach (var lfWs in lfWsList.Values)
			{
#if FW8_COMPAT
				IWritingSystem ws;
#else
				CoreWritingSystemDefinition ws;
#endif

				// It would be nice to call this to add to both analysis and vernacular WS.
				// But we need the flexibility of bringing in LF WS properties.
				/*
				if (!WritingSystemServices.FindOrCreateSomeWritingSystem(
					Cache, null, lfWs.Tag, true, true, out ws))
				{
					// Could neither find NOR create a writing system, probably because the tag was malformed? Log it and move on.
					Logger.Warning("Failed to find or create an Lcm writing system corresponding to tag {0}. Is it malformed?", lfWs.Tag);
					continue;
				}
				*/

				// Find the writing system the way every other LF tag is found before creating one.
				// LfMerge exports input systems by LanguageTag, while LCM knows a writing system by
				// its Id, and in older projects the two differ: spt-flex's input system "hi-IN" is
				// the writing system LCM holds as "hi-Deva-IN". GetOrSet looks a tag up only as an
				// Id, so it did not find it, created a duplicate "hi-IN" and put it in the current
				// lists; WsIdFromLfTag, which every multitext key goes through, then matched the
				// duplicate exactly, and text typed in LF went to it rather than to hi-Deva-IN.
				int existingHandle = LanguageTags.WsIdFromLfTag(wsManager, lfWs.Tag);
				bool wsAlreadyExisted;
				if (existingHandle != 0)
				{
					ws = wsManager.Get(existingHandle);
					wsAlreadyExisted = true;
				}
				else
				{
					// Nothing to find, so create it. GetOrSet creates under the canonical form of the
					// tag -- "qaa-x-qaa-v" becomes "qaa-x-v", "th-Thai" becomes "th" -- and adds the
					// writing system to the manager, so no Set() call is needed here.
					wsAlreadyExisted = wsManager.GetOrSet(lfWs.Tag, out ws);
				}

				// The WS does check that a property has a different value before setting it
				// (and thus setting IsChanged flag), but for Abbreviation the WS returns
				// Language if not set, and it fails to check that.
				if (ws.Abbreviation != lfWs.Abbreviation)
				{
					ws.Abbreviation = lfWs.Abbreviation;
				}
				ws.RightToLeftScript = lfWs.IsRightToLeft;
				// No wsManager.Replace(ws): ws is the manager's own object, so the edits above are
				// already in place. Replace looks the writing system up by LanguageTag, and where
				// the Id is spelled otherwise that lookup does harm. If it finds nothing, Replace
				// re-registers ws under a fresh handle mid-sync; if it finds another writing system
				// with that LanguageTag -- brb-flex-2022's unused "km-KH" beside "km-Khmr-KH" --
				// Replace evicts that one and gives ws its handle.

				if (!wsAlreadyExisted)
				{
					// LF doesn't distinguish between vernacular/analysis WS, so the roles are worked
					// out per project from its config -- see ClassifyVernacularWritingSystems. A
					// writing system can play both, "en" in lexeme and gloss alike being the usual
					// case, and one no field claims is analysis. Matching on the tag as LF spells it,
					// both sides being LF's own strings, so a non-canonical spelling still matches
					// itself.
					bool isVernacular = vernacularTags.Contains(lfWs.Tag);
					if (isVernacular)
						ServiceLocator.LanguageProject.AddToCurrentVernacularWritingSystems(ws);
					if (!isVernacular || analysisTags.Contains(lfWs.Tag))
						ServiceLocator.LanguageProject.AddToCurrentAnalysisWritingSystems(ws);
				}
			}
		}

		/// <summary>
		/// The best string for a single-string LCM field, and the writing system it is in. Null when
		/// LF holds no text in any writing system LCM has -- which is not the same as holding no text
		/// at all; see <see cref="BestTsStringFromMultiText"/>.
		/// </summary>
		private Tuple<string, int> BestStringAndWsFromMultiText(LfMultiText input, bool isAnalysisField = true)
		{
			if (input == null) return null;
			if (input.Count == 0)
			{
				// Some Language Forge fields, like scientificName on senses, have empty but
				// non-null multitexts; those should also be empty in LCM.
				return null;
			}

			IEnumerable<ILgWritingSystem> wsesToSearch = isAnalysisField ?
				ServiceLocator.LanguageProject.AnalysisWritingSystems :
				ServiceLocator.LanguageProject.VernacularWritingSystems;
//			List<Tuple<int, string>> wsesToSearch = isAnalysisField ?
//				_analysisWsIdsAndNamesInSearchOrder :
//				_vernacularWsIdsAndNamesInSearchOrder;

			// By handle, not by comparing LF's keys with ws.Id, which would miss a key LF spells
			// differently from LCM. Falls back to the first value in any writing system LCM knows.
			KeyValuePair<int, string> best = input.BestStringAndWsId(
				wsesToSearch.Select(ws => ws.Handle), ServiceLocator.WritingSystemFactory);
			if (best.Value == null)
				return null;
			return new Tuple<string, int>(best.Value, best.Key);
		}

		/// <summary>
		/// The value to give a single-string LCM field, whose current value is
		/// <paramref name="current"/>.
		///
		/// Null, clearing the field, when LF holds no text for it. But when LF holds text and every
		/// alternative is in a writing system LCM does not have, there is nowhere to put it, and the
		/// field keeps its current value. Neither alternative will do: building the string in
		/// writing system 0 throws, which abandons the rest of the entry half-written, and clearing
		/// the field would delete what LCM holds on account of text that could not be placed.
		/// </summary>
		private ITsString BestTsStringFromMultiText(LfMultiText input, ITsString current, bool isAnalysisField = true)
		{
			Tuple<string, int> stringAndWsId = BestStringAndWsFromMultiText(input, isAnalysisField);
			if (stringAndWsId != null)
				return ConvertMongoToLcmTsStrings.SpanStrToTsString(stringAndWsId.Item1, stringAndWsId.Item2, ServiceLocator.WritingSystemFactory);
			return NothingToPlace(input) ? null : current;
		}

		/// <summary>As <see cref="BestTsStringFromMultiText"/>, for a plain string field.</summary>
		private string BestStringFromMultiText(LfMultiText input, string current, bool isAnalysisField = true)
		{
			Tuple<string, int> stringAndWsId = BestStringAndWsFromMultiText(input, isAnalysisField);
			if (stringAndWsId != null)
				return stringAndWsId.Item1;
			return NothingToPlace(input) ? null : current;
		}

		/// <summary>
		/// Whether LF holds no text at all here. When it does hold text that nevertheless could not
		/// be placed, says so in the log, since that text is not reaching FieldWorks.
		/// </summary>
		private bool NothingToPlace(LfMultiText input)
		{
			if (input == null || input.IsEmpty)
				return true;
			Logger.Warning("MongoToLcm: text in writing system(s) {0} has nowhere to go, since LCM has none " +
				"of them; leaving the field as it was",
				string.Join(", ", input.Where(kv => kv.Value != null && !kv.Value.IsEmpty).Select(kv => kv.Key)));
			return false;
		}

		// This GetOrCreate() function takes an extra out parameter so we can correctly update
		// the entry counts in LfLexEntryToLcmLexEntry(). We don't update the counts here
		// because we don't yet know if the LF entry was deleted (in which case we wouldn't
		// want to update Added or Modified). The wantCreation parameter is there because if
		// the LF entry was deleted, we don't want to actually create the Lcm entry (it would
		// just be immediately deleted again).
		private ILexEntry GetOrCreateEntryByGuid(Guid guid, bool wantCreation, out bool createdEntry)
		{
			ILexEntry result;
			createdEntry = false;
			if (!GetInstance<ILexEntryRepository>().TryGetObject(guid, out result))
			{
				if (wantCreation)
				{
					createdEntry = true;
					result = GetInstance<ILexEntryFactory>().Create(guid, ServiceLocator.LanguageProject.LexDbOA);
					// TODO: Consider changing this to the following:
					// var msa = new SandboxGenericMSA();
					// result = GetInstance<ILexEntryFactory>().Create(GetInstance<IMoMorphTypeRepository>().GetObject(MoMorphTypeTags.kguidMorphStem), lexemeFormTs, (ITsString) null, msa);
					// However, this creates an empty Sense object -- and we already set the morph type when we set the LexemeFormOA. That should be enough.

					// If we do make that change, then the function signature will become:
					// private ILexEntry GetOrCreateEntryByGuid(Guid guid, bool wantCreation, ITsString lexemeFormTs, out bool createdEntry)
				}
				else
					result = null;
			}
			return result;
		}

		private ILexExampleSentence GetOrCreateExampleByGuid(Guid guid, ILexSense owner)
		{
			ILexExampleSentence result;
			if (!GetInstance<ILexExampleSentenceRepository>().TryGetObject(guid, out result))
				result = GetInstance<ILexExampleSentenceFactory>().Create(guid, owner);
			return result;
		}

		/// <summary>
		/// Gets or create the Lcm picture by GUID.
		/// </summary>
		/// <returns>The picture by GUID.</returns>
		/// <param name="guid">GUID.</param>
		/// <param name="owner">Owning sense</param>
		/// <param name="pictureName">Picture path name.</param>
		/// <param name="caption">Caption.</param>
		/// <param name="captionWs">Caption writing system.</param>
		private ICmPicture GetOrCreatePictureByGuid(Guid guid, ILexSense owner, string pictureName,
			string caption, int captionWs)
		{
			ICmPicture result;
			if (!GetInstance<ICmPictureRepository>().TryGetObject(guid, out result))
			{
				if (caption == null)
				{
					caption = "";
					captionWs = Cache.DefaultAnalWs;
				}
				ITsString captionTss = ConvertMongoToLcmTsStrings.SpanStrToTsString(caption, captionWs, ServiceLocator.WritingSystemFactory);
				result = GetInstance<ICmPictureFactory>().Create(guid);
				result.UpdatePicture(pictureName, captionTss, CmFolderTags.LocalPictures, captionWs);
				owner.PicturesOS.Add(result);
			}
			return result;
		}

		private ILexPronunciation GetOrCreatePronunciationByGuid(Guid guid, ILexEntry owner)
		{
			ILexPronunciation result;
			if (!GetInstance<ILexPronunciationRepository>().TryGetObject(guid, out result))
			{
				result = GetInstance<ILexPronunciationFactory>().Create();
				owner.PronunciationsOS.Add(result);
			}
			return result;
		}

		private ILexSense GetOrCreateSenseByGuid(Guid guid, ILexEntry owner)
		{
			ILexSense result;
			if (!GetInstance<ILexSenseRepository>().TryGetObject(guid, out result))
				result = GetInstance<ILexSenseFactory>().Create(guid, owner);
			return result;
		}

		private ICmTranslation FindOrCreateTranslationByGuid(Guid guid, ILexExampleSentence owner,
			ICmPossibility typeOfNewTranslation)
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

		private IMoForm CreateOwnedLexemeForm(ILexEntry owner, string morphologyType)
		{
			// morphologyType is a string because that's how it's (currently, as of Nov 2015)
			// stored in LF's Mongo database.
			IMoForm result;
			Guid morphGuid;
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
			case "phrase":
			case "discontiguous phrase":
			case "circumfix":
			// Also consider "stem" as the default in case of null or empty morph type
			case null:
			case "":
				morphGuid = MoMorphTypeTags.kguidMorphStem;
				break;

			// TODO: Decide if "phrase" and "discontiguous phrase" should be treated as stems the way FW's FindMorphType() function
			// does, or if we want to use MoMorphTypeTags.kguidMorphPhrase and MoMorphTypeTags.kguidMorphDiscontiguousPhrase
			// even though FieldWorks doesn't appear to use those.

			case "clitic":
				morphGuid = MoMorphTypeTags.kguidMorphClitic;
				break;
			case "proclitic":
				morphGuid = MoMorphTypeTags.kguidMorphProclitic;
				break;
			case "enclitic":
				morphGuid = MoMorphTypeTags.kguidMorphEnclitic;
				break;
			case "prefix":
			case "prefixing interfix":  // Lcm's MorphServices prefers to consider "prefixing interfix" as a prefix
				morphGuid = MoMorphTypeTags.kguidMorphPrefix;
				break;
			case "infix":
			case "infixing interfix":  // Lcm's MorphServices prefers to consider "infixing interfix" as an infix
				morphGuid = MoMorphTypeTags.kguidMorphInfix;
				break;
			case "suffix":
			case "suffixing interfix":  // Lcm's MorphServices prefers to consider "suffixing interfix" as a suffix
				morphGuid = MoMorphTypeTags.kguidMorphSuffix;
				break;
			case "simulfix":
				morphGuid = MoMorphTypeTags.kguidMorphSimulfix;
				break;
			case "suprafix":
				morphGuid = MoMorphTypeTags.kguidMorphSuprafix;
				break;
			default:
				Logger.Warning("Unrecognized morphology type \"{0}\" in word {1}", morphologyType, owner.Guid);
				morphGuid = MoMorphTypeTags.kguidMorphStem;
				break;
			}
			if (morphGuid == MoMorphTypeTags.kguidMorphStem)
				result = stemFactory.Create();
			else
				result = affixFactory.Create();
			owner.LexemeFormOA = result;  // MUST do this assignment *before* assigning result.MorphTypeRA, otherwise Lcm throws a NullReferenceException
			result.MorphTypeRA = GetInstance<IMoMorphTypeRepository>().GetObject(morphGuid);
			Logger.Debug("Just set LexemeFormOA to {0}, with MorphType {1}", result == null ? "(null)" : result.ToString(), result == null ? "(null lexeme form)" : result.MorphTypeRA == null ? "(null morphtype)" : result.MorphTypeRA.ToString());
			return result;
		}

		/// <summary>
		/// The last sync's date, or null if the project has never been synced.
		/// </summary>
		private DateTime? LastSyncedDate
		{
			get
			{
				DateTime? lastSynced = ProjectRecord.LastSyncedDate;
				return lastSynced == null || lastSynced.Value <= MagicValues.UnixEpoch ? null : lastSynced;
			}
		}

		/// <summary>
		/// Whether an LF user has edited, created or deleted this entry since the last sync, or ever,
		/// if the project has never been synced.
		///
		/// Mongo holds FieldWorks as it was at the last sync plus what LF users have done since, so
		/// those are the only entries LF has anything to say about. Comparing LF's copy with
		/// FieldWorks's instead is only right while LfMerge's working copy is still exactly what it
		/// last synced: on a fresh clone of a project FLEx has kept working on, every entry FLEx
		/// changed since then would look changed in LF, and LF's older copy would be written over it.
		///
		/// LF stamps an entry's DateModified on every edit and deletion (MapperModel::write), and
		/// LfMerge's own writes to Mongo all come before it records LastSyncedDate. Both dates come
		/// from the server's clock, so a FLEx user's clock being wrong cannot affect this. Mongo keeps
		/// them to the millisecond, and a tie counts as edited: that falls back to comparing LF's copy
		/// with FieldWorks's, which is what LfMerge has always done.
		///
		/// A project that has never been synced has no date to go by, though its entries can still be
		/// LfMerge's: the initial clone's transfer to Mongo does not always get as far as recording a
		/// sync. LfMerge clears the user references on every entry it writes, and LF sets one on every
		/// edit and creation; a deletion sets none, but deleting what FieldWorks lacks is harmless.
		/// </summary>
		private bool EditedInLfSinceLastSync(LfLexEntry lfEntry)
		{
			DateTime? lastSynced = LastSyncedDate;
			if (lastSynced == null)
				return lfEntry.IsDeleted || (lfEntry.AuthorInfo != null &&
					(lfEntry.AuthorInfo.ModifiedByUserRef != null || lfEntry.AuthorInfo.CreatedByUserRef != null));
			return lfEntry.DateModified >= lastSynced.Value;
		}

		private void LfLexEntryToLcmLexEntry(LfLexEntry lfEntry)
		{
			Guid guid = lfEntry.Guid ?? Guid.Empty;
			if (!EditedInLfSinceLastSync(lfEntry))
			{
				// Nothing for FieldWorks here. Where it differs, it is the newer one, and the transfer
				// back to Mongo after Send/Receive will bring LF up to date with it.
				ILexEntry existing;
				if (lfEntry.IsDeleted)
				{
					// An LF deletion that an earlier sync has already sent
				}
				else if (!GetInstance<ILexEntryRepository>().TryGetObject(guid, out existing))
					_absentOnlyFromFieldWorks++;
				else if (lfEntry.AuthorInfo == null || lfEntry.AuthorInfo.ModifiedDate.ToLocalTime() != existing.DateModified)
					_changedOnlyInFieldWorks++;
				return;
			}
			bool createdEntry = false;
			bool wantCreation = !lfEntry.IsDeleted;
			ILexEntry LcmEntry = GetOrCreateEntryByGuid(guid, wantCreation, out createdEntry);
			if (lfEntry.IsDeleted)
			{
				// LF entry deleted: delete the corresponding Lcm entry
				if (LcmEntry == null)
					return; // No need to delete an Lcm entry that doesn't exist
				if (LcmEntry.CanDelete)
				{
					if (createdEntry)
					{
						// This Lcm entry, which was deleted in LF, was apparently "created" by Lcm.
						// In reality, it was created just to be deleted, and we should optimize that away.
						Logger.Warning("LfMerge managed to create Lcm entry {0} just to immediately delete it again. This is inefficient and should be fixed.",
							LcmEntry.Guid);
					}
					else
					{
						EntryCounts.Deleted++;
					}
					Logger.Info("MongoToLcm: Deleted LcmEntry {0} ({1})", guid, ConvertUtilities.EntryNameForDebugging(lfEntry));
					LcmEntry.Delete();
				}
				else
				{
					Logger.Warning("Problem: need to delete Lcm entry {0}, but its CanDelete flag is false.",
						LcmEntry.Guid);
				}
				return; // Don't set fields on a deleted entry
			}
			// Has LF entry changed since last time we set Lcm values?
			if (lfEntry.AuthorInfo.ModifiedDate.ToLocalTime() == LcmEntry.DateModified)
			{
				if (createdEntry)
				{
					// Entry was created in LF, so we need to create & populate it in Lcm. Don't skip it.
				}
				else
				{
					// No changes detected since last time, so we won't change the Lcm entry
					return;
				}
			}
			if (!createdEntry && LastSyncedDate != null && LcmEntry.DateModified.ToUniversalTime() > LastSyncedDate.Value)
			{
				// Only possible when the working copy has moved on without LF (or a FLEx user's clock
				// is ahead). LF's edit still wins, as it always has; this records what it replaced.
				Logger.Warning("MongoToLcm: entry {0} ({1}) was edited in FieldWorks on {2:u} as well as in LF since the last sync; " +
					"LF's version replaces FieldWorks's", guid, ConvertUtilities.EntryNameForDebugging(lfEntry),
					LcmEntry.DateModified.ToUniversalTime());
			}

			// Fields in order by lfEntry property, except for Senses and CustomFields, which are handled at the end
			SetMultiStringFrom(LcmEntry.CitationForm, lfEntry.CitationForm);

			// DateModified and DateCreated can be confusing, because LF and Lcm are doing two different
			// things with them. In Lcm, there is just one DateModified and one DateCreated; simple. But
			// in LF, there is an AuthorInfo record as well, which contains its own ModifiedDate and CreatedDate
			// fields. (Note the word order: there's LfEntry.DateCreated, and LfEntry.AuthorInfo.CreatedDate).

			// The conversion we have chosen to use is: AuthorInfo will correspond to Lcm. So Lcm.DateCreated
			// becomes AuthorInfo.CreatedDate, and Lcm.DateModified becomes AuthorInfo.ModifiedDate. The two
			// fields on the LF entry will instead refer to when the *Mongo record* was created or modified,
			// and the LfEntry.DateCreated and LfEntry.DateModified fields will never be put into Lcm.

			// Use AuthorInfo for dates as this should always reflect user changes (Mongo or Lcm)
			// Weirdly, Lcm expects Dates to be in LOCAL time, not UTC.
			if (lfEntry.AuthorInfo != null)
			{
				LcmEntry.DateCreated = lfEntry.AuthorInfo.CreatedDate.ToLocalTime();
				LcmEntry.DateModified = lfEntry.AuthorInfo.ModifiedDate.ToLocalTime();
			}

			SetMultiStringFrom(LcmEntry.Bibliography, lfEntry.EntryBibliography);
			SetMultiStringFrom(LcmEntry.Restrictions, lfEntry.EntryRestrictions);
			SetEtymologyFields(LcmEntry, lfEntry);
			SetLexeme(LcmEntry, lfEntry);
			// LcmEntry.LIFTid = lfEntry.LiftId; // TODO: Figure out how to handle this one.
			SetMultiStringFrom(LcmEntry.LiteralMeaning, lfEntry.LiteralMeaning);
			SetMultiStringFrom(LcmEntry.Comment, lfEntry.Note);
			SetPronunciation(LcmEntry, lfEntry);
			SetMultiStringFrom(LcmEntry.SummaryDefinition, lfEntry.SummaryDefinition);
			// TODO: Do something like the following line (can't do exactly that because PrimaryMorphType is read-only)
			// LcmEntry.PrimaryMorphType = new PossibilityListConverter(LcmEntry.PrimaryMorphType.OwningList).GetByName(lfEntry.MorphologyType) as IMoMorphType;

			/* LfLexEntry fields not mapped:
			lfEntry.Environments // Don't know how to handle this one. TODO: Research it.
			lfEntry.LiftId // TODO: Figure out how to handle this one. In LcmEntry, it's a constructed value.
			lfEntry.MercurialSha; // Skip: We don't update this until we've committed to the Mercurial repo
			*/

			// lfEntry.Senses -> LcmEntry.SensesOS
			SetLcmListFromLfList(LcmEntry, LcmEntry.SensesOS, lfEntry.Senses, LfSenseToLcmSense);

			_convertCustomField.SetCustomFieldsForThisCmObject(LcmEntry, "entry", lfEntry.CustomFields,
				lfEntry.CustomFieldGuids);

			// If we got this far, we either created or modified this entry
			if (createdEntry)
				EntryCounts.Added++;
			else
				EntryCounts.Modified++;

			Logger.Info("MongoToLcm: {0} LcmEntry {1} ({2})", createdEntry ? "Created" : "Modified", guid, ConvertUtilities.EntryNameForDebugging(lfEntry));
		}

		private void LfExampleToLcmExample(LfExample lfExample, ILexSense owner)
		{
			Guid guid = lfExample.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfExample.LiftId);
			ILexExampleSentence LcmExample = GetOrCreateExampleByGuid(guid, owner);
			// Ignoring lfExample.AuthorInfo.CreatedDate;
			// Ignoring lfExample.AuthorInfo.ModifiedDate;
			// Ignoring lfExample.ExampleId; // TODO: is this different from a LIFT ID?
			SetMultiStringFrom(LcmExample.Example, lfExample.Sentence);
//			Logger.Debug("Lcm Example just got set to {0} for GUID {1} and HVO {2}",
//				ConvertLcmToMongoTsStrings.SafeTsStringText(LcmExample.Example.BestAnalysisVernacularAlternative),
//				LcmExample.Guid,
//				LcmExample.Hvo
//			);
			LcmExample.Reference = BestTsStringFromMultiText(lfExample.Reference, LcmExample.Reference);
			ICmTranslation t = FindOrCreateTranslationByGuid(lfExample.TranslationGuid, LcmExample,
				_freeTranslationType);
			SetMultiStringFrom(t.Translation, lfExample.Translation);
			// Ignoring t.Status since LF won't touch it

			_convertCustomField.SetCustomFieldsForThisCmObject(LcmExample, "examples",
				lfExample.CustomFields, lfExample.CustomFieldGuids);
		}

		/// <summary>
		/// Converts LF picture into Lcm picture.  Internal Lcm pictures will need to have the
		/// directory path "Pictures/" prepended to the filename.  Externally linked picture names
		/// won't be modifed.
		/// </summary>
		/// <param name="lfPicture">Lf picture.</param>
		/// <param name="owner">Owning sense.</param>
		private void LfPictureToLcmPicture(LfPicture lfPicture, ILexSense owner)
		{
			if (lfPicture == null || lfPicture.FileName == null)
				return;  // Do nothing if there's no picture to convert
			Guid guid = lfPicture.Guid ?? Guid.Empty;
			int captionWs = Cache.DefaultAnalWs;
			string caption = "";
			if (lfPicture.Caption != null)
			{
				// A caption in no writing system LCM has would come back as writing system 0, which
				// cannot be built into a string; the picture then starts with an empty caption, and
				// SetMultiStringFrom below fills in every alternative that can be placed.
				KeyValuePair<int, string> kv = lfPicture.Caption.BestStringAndWsId(
					ServiceLocator.LanguageProject.AnalysisWritingSystems.Select(ws => ws.Handle),
					ServiceLocator.WritingSystemFactory);
				if (kv.Value != null)
				{
					captionWs = kv.Key;
					caption = kv.Value;
				}
			}

			// Lcm expects internal pictures in a certain path.  If an external path already
			// exists, leave it alone.
			string pictureName = lfPicture.FileName;
			Regex regex = new Regex(@"[/\\]");
			const string LcmPicturePath = "Pictures/";
			string picturePath = regex.Match(pictureName).Success ? pictureName :
				string.Format("{0}{1}", LcmPicturePath, pictureName);

			ICmPicture LcmPicture = GetOrCreatePictureByGuid(guid, owner, picturePath, caption, captionWs);
			// Lcm currently only allows one caption to be created with the picture, so set the
			// other captions afterwards
			SetMultiStringFrom(LcmPicture.Caption, lfPicture.Caption);
			// Ignoring LcmPicture.Description and other LcmPicture fields since LF won't touch them
		}

		private void LfSenseToLcmSense(LfSense lfSense, ILexEntry owner)
		{
			Guid guid = lfSense.Guid ?? Guid.Empty;
			if (guid == Guid.Empty)
				guid = GuidFromLiftId(lfSense.LiftId);
			ILexSense LcmSense = GetOrCreateSenseByGuid(guid, owner);

			// Set the Guid on the LfSense object, so we can later track it for deletion purposes
			// (see LfEntryToLcmEntry)
			lfSense.Guid = LcmSense.Guid;

			ListConverters[AcademicDomainListCode].UpdatePossibilitiesFromStringArray(LcmSense.DomainTypesRC,
				lfSense.AcademicDomains);
			ListConverters[AnthroCodeListCode].UpdatePossibilitiesFromStringArray(LcmSense.AnthroCodesRC,
				lfSense.AnthropologyCategories);
			SetMultiStringFrom(LcmSense.AnthroNote, lfSense.AnthropologyNote);
			// Ignoring lfSense.AuthorInfo.CreatedDate;
			// Ignoring lfSense.AuthorInfo.ModifiedDate;
			SetMultiStringFrom(LcmSense.Definition, lfSense.Definition);
			SetMultiStringFrom(LcmSense.DiscourseNote, lfSense.DiscourseNote);
			SetMultiStringFrom(LcmSense.EncyclopedicInfo, lfSense.EncyclopedicNote);
			SetMultiStringFrom(LcmSense.GeneralNote, lfSense.GeneralNote);
			SetMultiStringFrom(LcmSense.Gloss, lfSense.Gloss);
			SetMultiStringFrom(LcmSense.GrammarNote, lfSense.GrammarNote);
			// LcmSense.LIFTid = lfSense.LiftId; // Read-only property in Lcm Sense, doesn't make
			// sense to set it. TODO: Is that correct?
			IPartOfSpeech pos = ConvertPos(lfSense.PartOfSpeech, lfSense);
			if (pos != null)
			{
				IPartOfSpeech secondaryPos = ConvertPos(lfSense.SecondaryPartOfSpeech, lfSense); // Only used in derivational affixes, will be null otherwise
				if (LcmSense.MorphoSyntaxAnalysisRA == null)
				{
					// If we've got a brand-new LcmSense object, we'll need to create a new MSA for it.
					// That's what the SandboxGenericMSA class is for: assigning it to the SandboxMSA
					// member of LcmSense will automatically create an MSA of the correct class. Handy!
					MsaType msaType = LcmSense.GetDesiredMsaType();
					SandboxGenericMSA sandboxMsa = SandboxGenericMSA.Create(msaType, pos);
					if (secondaryPos != null)
						sandboxMsa.SecondaryPOS = secondaryPos;
					LcmSense.SandboxMSA = sandboxMsa;
				}
				else
				{
					ConvertMongoToLcmPartsOfSpeech.SetPartOfSpeech(LcmSense.MorphoSyntaxAnalysisRA,
						pos, secondaryPos, Logger); // It's fine if secondaryPos is null
				}
			}
			SetMultiStringFrom(LcmSense.PhonologyNote, lfSense.PhonologyNote);
			// LcmSense.ReversalEntriesRC = lfSense.ReversalEntries; // TODO: More complex than
			// that. Handle it correctly. Maybe.
			LcmSense.ScientificName = BestTsStringFromMultiText(lfSense.ScientificName, LcmSense.ScientificName);
			ListConverters[SemDomListCode].UpdatePossibilitiesFromStringArray(LcmSense.SemanticDomainsRC,
				lfSense.SemanticDomain);
			SetMultiStringFrom(LcmSense.SemanticsNote, lfSense.SemanticsNote);
			SetMultiStringFrom(LcmSense.Bibliography, lfSense.SenseBibliography);

			// lfSense.SenseId; // TODO: What do I do with this one?
			LcmSense.ImportResidue = BestTsStringFromMultiText(lfSense.SenseImportResidue, LcmSense.ImportResidue);

			SetMultiStringFrom(LcmSense.Restrictions, lfSense.SenseRestrictions);
			LcmSense.SenseTypeRA = ListConverters[SenseTypeListCode].FromStringField(lfSense.SenseType);
			SetMultiStringFrom(LcmSense.SocioLinguisticsNote, lfSense.SociolinguisticsNote);
			LcmSense.Source = BestTsStringFromMultiText(lfSense.Source, LcmSense.Source);
			LcmSense.StatusRA = ListConverters[StatusListCode].FromStringArrayFieldWithOneCase(lfSense.Status);
			ListConverters[UsageTypeListCode].UpdatePossibilitiesFromStringArray(LcmSense.UsageTypesRC,
				lfSense.Usages);

			// lfSense.Examples -> LcmSense.ExamplesOS
			SetLcmListFromLfList(LcmSense, LcmSense.ExamplesOS, lfSense.Examples, LfExampleToLcmExample);

			// lfSense.Pictures -> LcmSense.PicturesOS
			SetLcmListFromLfList(LcmSense, LcmSense.PicturesOS, lfSense.Pictures, LfPictureToLcmPicture);

			_convertCustomField.SetCustomFieldsForThisCmObject(LcmSense, "senses", lfSense.CustomFields,
				lfSense.CustomFieldGuids);
		}

		// Given a list of LF objects that are "owned" by a parent object (e.g., LfSense.Examples)
		// and the corresponding Lcm list (e.g., ILexSense.ExamplesOS), convert the LF list to Lcm
		// (with the conversion function passed in as a parameter).
		// Then go through the Lcm list and look for any objects that were NOT in the LF list
		// (identifying them by their Guid) and delete them, because their absence from LF means
		// that they were deleted in LF at some point in the past. In addition to the two lists,
		// the Lcm parent object is also required, because the conversion needs it as a parameter.
		//
		// This is a pattern that we use several times in the Mongo->Lcm conversion
		// (LfSense.Examples, LfSense.Pictures, LfEntry.Senses), so this function exists to
		// generalize that pattern.
		private void SetLcmListFromLfList<TLfChild, TLcmParent, TLcmChild>(
			TLcmParent LcmParent,
			ILcmOwningSequence<TLcmChild> LcmChildList,
			IList<TLfChild> lfChildList,
			Action<TLfChild, TLcmParent> convertAction
		)
			where TLcmParent : ICmObject
			where TLcmChild : class, ICmObject
			where TLfChild : IHasNullableGuid
		{
			var guidsFoundInLf = new HashSet<Guid>();
			var guidOrderFromLf = new List<Guid>();
			var objectsToDeleteFromLcm = new HashSet<TLcmChild>();
			var LcmChildObjectsByGuid = new Dictionary<Guid, TLcmChild>();
			foreach (TLfChild lfChild in lfChildList)
			{
				convertAction(lfChild, LcmParent);
				Logger.Debug("After running convert action, LfChild's GUID was {0}", (lfChild.Guid == null ? "(null)" : lfChild.Guid.Value.ToString()));
				if (lfChild.Guid != null) {
					guidsFoundInLf.Add(lfChild.Guid.Value);
					guidOrderFromLf.Add(lfChild.Guid.Value);
				}
			}
			// Any Lcm objects that DON'T have a corresponding Guid in LF should now be deleted
			foreach (TLcmChild LcmChild in LcmChildList)
			{
				LcmChildObjectsByGuid.Add(LcmChild.Guid, LcmChild);
				if (!guidsFoundInLf.Contains(LcmChild.Guid))
					// Don't delete them yet, as that could change the list we're iterating over
					objectsToDeleteFromLcm.Add(LcmChild);
			}
			// Now it's safe to delete them
			foreach (TLcmChild LcmChildToDelete in objectsToDeleteFromLcm)
				LcmChildToDelete.Delete();

			// Now rearrange the Lcm list to match the order of the LF list
			Logger.Debug("About to rearrange order for list {0}", LcmParent.Guid);
			int i = 0;
			foreach (Guid guid in guidOrderFromLf) {
				TLcmChild item;
				if (LcmChildObjectsByGuid.TryGetValue(guid, out item)) {
					// Note that we can't use MoveTo() since LcmOwningSequence explicitly doesn't handle
					// the case where the object is moving from the same list. But its Insert() implementation
					// handles that case, and actually does a *move* rather than inserting the item twice.

					Logger.Debug("Inserting {0} at index {1} in list {2}", item.Guid, i, LcmParent.Guid);
					LcmChildList.Insert(i, item);
				}
				i++;
			}
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
		/// Sets all writing systems in an Lcm multi string from a LanguageForge MultiText field.
		/// Destination is first parameter, like the order of an assignment statement.
		/// </summary>
		/// <param name="dest">Lcm multi string whose values will be set.</param>
		/// <param name="source">Source of multistring values.</param>
		private void SetMultiStringFrom(IMultiStringAccessor dest, LfMultiText source)
		{
			if (source == null)
				ClearMultiString(dest);
			else
				source.WriteToLcmMultiString(dest, ServiceLocator.WritingSystemManager);
		}

		/// <summary>
		/// Clears all text in all writing systems in an Lcm MultiString object
		/// </summary>
		private void ClearMultiString(IMultiStringAccessor multiString)
		{
			if (multiString == null) return;
			foreach (int wsId in multiString.AvailableWritingSystemIds)
				multiString.set_String(wsId, string.Empty);
		}

		private void SetEtymologyFields(ILexEntry LcmEntry, LfLexEntry lfEntry)
		{
#if DBVERSION_7000068
			var LcmEtymology = LcmEntry.EtymologyOA;
#else
			var LcmEtymology = LcmEntry.EtymologyOS.FirstOrDefault();
#endif
			if ((lfEntry.Etymology        == null || lfEntry.Etymology       .IsEmpty) &&
			    (lfEntry.EtymologyComment == null || lfEntry.EtymologyComment.IsEmpty) &&
			    (lfEntry.EtymologyGloss   == null || lfEntry.EtymologyGloss  .IsEmpty) &&
			    (lfEntry.EtymologySource  == null || lfEntry.EtymologySource .IsEmpty))
			{
				if (LcmEtymology == null)
					return; // Don't delete an Etymology object if there was none already
#if DBVERSION_7000068
				LcmEtymology.Delete();
#else
				LcmEntry.EtymologyOS.First().Delete();
#endif
				return;
			}
			if (LcmEtymology == null)
			{
				LcmEtymology = GetInstance<ILexEtymologyFactory>().Create();
#if DBVERSION_7000068
				LcmEntry.EtymologyOA = LcmEtymology;
#else
				LcmEntry.EtymologyOS.Add(LcmEtymology);
#endif
			}

			SetMultiStringFrom(LcmEtymology.Form, lfEntry.Etymology);
			SetMultiStringFrom(LcmEtymology.Comment, lfEntry.EtymologyComment);
			SetMultiStringFrom(LcmEtymology.Gloss, lfEntry.EtymologyGloss);
			if (lfEntry.EtymologySource != null)
#if DBVERSION_7000068
				LcmEtymology.Source = BestStringFromMultiText(lfEntry.EtymologySource, LcmEtymology.Source);
#else
				SetMultiStringFrom(LcmEtymology.LanguageNotes, lfEntry.EtymologySource);
#endif
		}

		private void SetLexeme(ILexEntry LcmEntry, LfLexEntry lfEntry)
		{
			IMoForm LcmLexeme = LcmEntry.LexemeFormOA;
			if (LcmLexeme == null)
				LcmLexeme = CreateOwnedLexemeForm(LcmEntry, lfEntry.MorphologyType); // Also sets owning field on LcmEntry
			if (lfEntry.Lexeme == null || lfEntry.Lexeme.IsEmpty)
			{
				ClearMultiString(LcmLexeme.Form);
				return;
			}
			// TODO: Fold the "ClearMultiString" logic into SetMultiStringFrom so that it will *reset* any MultiString fields that aren't there in LF.
			// TODO: But first, check if that's necessary.
			SetMultiStringFrom(LcmLexeme.Form, lfEntry.Lexeme);
		}

		private void SetPronunciation(ILexEntry LcmEntry, LfLexEntry lfEntry)
		{
			// var LcmPronunciation = GetOrCreatePronunciationByGuid(lfEntry.PronunciationGuid, LcmEntry);
			ILexPronunciation LcmPronunciation = LcmEntry.PronunciationsOS.FirstOrDefault();
			if ((lfEntry.Pronunciation == null || lfEntry.Pronunciation.IsEmpty) &&
			    (lfEntry.CvPattern     == null || lfEntry.CvPattern    .IsEmpty) &&
			    (lfEntry.Tone          == null || lfEntry.Tone         .IsEmpty) &&
			    (lfEntry.Location      == null || lfEntry.Location     .IsEmpty))
			{
				// No pronunication at all in LF: either there was never one, or we deleted it
				if (LcmPronunciation == null)
					return;  // There was never a pronunciation; we're fine
				else
					LcmEntry.PronunciationsOS.First().Delete();
					return;
			}
			if (LcmPronunciation == null)
			{
				LcmPronunciation = GetInstance<ILexPronunciationFactory>().Create();
				LcmEntry.PronunciationsOS.Add(LcmPronunciation);
			}
			LcmPronunciation.CVPattern = BestTsStringFromMultiText(lfEntry.CvPattern, LcmPronunciation.CVPattern);
			LcmPronunciation.Tone = BestTsStringFromMultiText(lfEntry.Tone, LcmPronunciation.Tone);
			SetMultiStringFrom(LcmPronunciation.Form, lfEntry.Pronunciation);
			LcmPronunciation.LocationRA =
				(ICmLocation)ListConverters[LocationListCode].FromStringField(lfEntry.Location);
			// Not handling LcmPronunciation.MediaFilesOS. TODO: At some point we may want to handle
			// media files as well.
			// Not handling LcmPronunciation.LiftResidue
		}

		private IPartOfSpeech ConvertPos(LfStringField source, LfSense owner)
		{
			return ListConverters[GrammarListCode].FromStringField(source) as IPartOfSpeech;
		}
	}
}
