// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
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

		public bool InitialClone { get; set; }

		public int _wsEn;

		public ConvertCustomField _convertCustomField;

		public ConvertFdoToMongoOptionList _convertGrammaticalCategoryOptionList;
		public ConvertFdoToMongoOptionList _convertSenseTypeOptionList;
		public ConvertFdoToMongoOptionList _convertLocationOptionList;

		public ConvertFdoToMongoLexicon(ILfProject lfProject, bool initialClone, ILogger logger, IMongoConnection connection)
		{
			LfProject = lfProject;
			InitialClone = initialClone;
			Logger = logger;
			Connection = connection;

			FwProject = LfProject.FieldWorksProject;
			Cache = FwProject.Cache;
			_wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");

			_convertCustomField = new ConvertCustomField(Cache);

			// Reconcile writing systems from FDO and Mongo
			Dictionary<string, LfInputSystemRecord> lfWsList = FdoWsToLfWs();
			CoreWritingSystemDefinition VernacularWs = Cache.LanguageProject.DefaultVernacularWritingSystem;
			CoreWritingSystemDefinition AnalysisWs = Cache.LanguageProject.DefaultAnalysisWritingSystem;
			Logger.Debug("Vernacular {0}, Analysis {1}", VernacularWs, AnalysisWs);
			Connection.SetInputSystems(LfProject, lfWsList, InitialClone, VernacularWs.Id, AnalysisWs.Id);

			var fdoPartsOfSpeech = Cache.LanguageProject.PartsOfSpeechOA;
			_convertGrammaticalCategoryOptionList = ConvertOptionListFromFdo(LfProject, MagicStrings.LfOptionListCodeForGrammaticalInfo, fdoPartsOfSpeech);

			var fdoSenseType = Cache.LanguageProject.LexDbOA.SenseTypesOA;
			_convertSenseTypeOptionList = ConvertOptionListFromFdo(LfProject, MagicStrings.LfOptionListCodeForSenseTypes, fdoSenseType);

			var fdoLocation = Cache.LanguageProject.LocationsOA;
			_convertLocationOptionList = ConvertOptionListFromFdo(LfProject, MagicStrings.LfOptionListCodeForLocations, fdoSenseType);

			//Dictionary<string, LfConfigFieldBase>_lfCustomFieldList = new Dictionary<string, LfConfigFieldBase>();

		}

		public void RunConversion()
		{
			Logger.Notice("FdoToMongo: LexEntryRepository");
			ILexEntryRepository repo = GetInstance<ILexEntryRepository>();
			if (repo == null)
			{
				Logger.Error("Can't find LexEntry repository for FieldWorks project {0}", LfProject.FwProjectCode);
				return;
			}

			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				LfLexEntry lfEntry = FdoLexEntryToLfLexEntry(fdoEntry);
				Logger.Info("Populated LfEntry {0}", lfEntry.Guid);
				Connection.UpdateRecord(LfProject, lfEntry);
			}
			InitialClone = false;

			IEnumerable<LfLexEntry> lfEntries = Connection.GetRecords<LfLexEntry>(LfProject, MagicStrings.LfCollectionNameForLexicon);
			List<Guid> entryGuidsToRemove = new List<Guid>();
			foreach (LfLexEntry lfEntry in lfEntries)
			{
				if (lfEntry.Guid == null)
					continue;
				if (!Cache.ServiceLocator.ObjectRepository.IsValidObjectId(lfEntry.Guid.Value))
					entryGuidsToRemove.Add(lfEntry.Guid.Value);
			}
			foreach (Guid guid in entryGuidsToRemove)
			{
				Connection.RemoveRecord(LfProject, guid);
			}


			lfEntries = Connection.GetRecords<LfLexEntry>(LfProject, MagicStrings.LfCollectionNameForLexicon);
			var LfCustomFieldEntryList = lfEntries.Select(e => e.CustomFields).Where(c => c != null && c.Count() > 0);
			Console.WriteLine("custom fields is {0}", LfCustomFieldEntryList);
			foreach (var LfCustomFieldEntryName in LfCustomFieldEntryList.First().Names)
			{
				Console.WriteLine("custom field entry name is {0}", LfCustomFieldEntryName);
			}

			// Update custom fields for project config
			IEnumerable<LfLexEntry>lfCustomFieldEntries = lfEntries.Where(e => e.CustomFields.Count() > 0);
			Console.WriteLine("lfCustomFieldEntries is {0}", lfCustomFieldEntries);

			// During initial clone, use initial FDO view preferences for custom fields.
			//var lfCustomFieldEntry = FdoCustomFieldToLfCustomField();

			#region custom field
			Connection.SetCustomFieldConfig(LfProject);
			#endregion


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

		static public LfMultiText ToMultiText(IMultiAccessorBase fdoMultiString, WritingSystemManager fdoWritingSystemManager)
		{
			if ((fdoMultiString == null) || (fdoWritingSystemManager == null)) return null;
			return LfMultiText.FromFdoMultiString(fdoMultiString, fdoWritingSystemManager);
		}

		public LfLexEntry FdoLexEntryToLfLexEntry(ILexEntry fdoEntry)
		{
			if (fdoEntry == null) return null;
			Logger.Notice("Converting FDO LexEntry with GUID {0}", fdoEntry.Guid);

			CoreWritingSystemDefinition AnalysisWritingSystem = Cache.LanguageProject.DefaultAnalysisWritingSystem;
			// string VernacularWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(Cache.DefaultVernWs);

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
			lfEntry.CitationForm = ToMultiText(fdoEntry.CitationForm);
			lfEntry.Note = ToMultiText(fdoEntry.Comment);

			lfEntry.DateCreated = fdoEntry.DateCreated;
			lfEntry.DateModified = fdoEntry.DateModified;
			if (InitialClone)
			{
				var now = DateTime.Now;
				lfEntry.DateCreated = now;
				lfEntry.DateModified = now;
			}
			// TODO: In some LIFT imports, AuthorInfo.CreatedDate in Mongo doesn't match fdoEntry.DateCreated. Figure out why.
			if (lfEntry.AuthorInfo == null)
				lfEntry.AuthorInfo = new LfAuthorInfo();
			lfEntry.AuthorInfo.CreatedByUserRef = null;
			lfEntry.AuthorInfo.CreatedDate = fdoEntry.DateCreated;
			lfEntry.AuthorInfo.ModifiedByUserRef = null;
			lfEntry.AuthorInfo.ModifiedDate = fdoEntry.DateModified;

			ILexEtymology fdoEtymology = fdoEntry.EtymologyOA;
			if (fdoEtymology != null)
			{
				lfEntry.Etymology = ToMultiText(fdoEtymology.Form); // TODO: Check if ILexEtymology.Form is the right field here
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
				lfEntry.PronunciationGuid = fdoPronunciation.Guid;
				lfEntry.Pronunciation = ToMultiText(fdoPronunciation.Form);
				lfEntry.CvPattern = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem.Id, fdoPronunciation.CVPattern);
				lfEntry.Tone = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem.Id, fdoPronunciation.Tone);
				// TODO: Map fdoPronunciation.MediaFilesOS properly (converting video to sound files if necessary)
				//lfEntry.Location = LfStringField.FromString(fdoPronunciation.LocationRA.AbbrAndName);
				lfEntry.Location = LfStringField.FromString(_convertLocationOptionList.LfItemKeyString(fdoPronunciation.LocationRA, _wsEn));
			}
			lfEntry.EntryRestrictions = ToMultiText(fdoEntry.Restrictions);
			if (lfEntry.Senses == null) // Shouldn't happen, but let's be careful
				lfEntry.Senses = new List<LfSense>();
			lfEntry.Senses.AddRange(fdoEntry.SensesOS.Select(FdoSenseToLfSense));
			lfEntry.SummaryDefinition = ToMultiText(fdoEntry.SummaryDefinition);

			BsonDocument customFieldsAndGuids;
			string lfCustomFieldType;
			LfConfigFieldBase lfCustomFieldSettings;
			_convertCustomField.getCustomFieldsForThisCmObject(fdoEntry, "entry",
				out customFieldsAndGuids, out lfCustomFieldType, out lfCustomFieldSettings);
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
			fdoEntry.CitationFormWithAffixType; // TODO: This one should maybe be mapped. Figure out how.
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

		public LfSense FdoSenseToLfSense(ILexSense fdoSense)
		{
			var lfSense = new LfSense();

			CoreWritingSystemDefinition VernacularWritingSystem = Cache.LanguageProject.DefaultVernacularWritingSystem;
			CoreWritingSystemDefinition AnalysisWritingSystem = Cache.LanguageProject.DefaultAnalysisWritingSystem;

			// TODO: Currently skipping subsenses. Figure out if we should include them or not.

			lfSense.Guid = fdoSense.Guid;
			lfSense.Gloss = ToMultiText(fdoSense.Gloss);
			lfSense.Definition = ToMultiText(fdoSense.Definition);

			// Fields below in alphabetical order by ILexSense property, except for Guid, Gloss and Definition
			lfSense.AcademicDomains = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.DomainTypesRC);
			lfSense.AnthropologyCategories = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.AnthroCodesRC);
			lfSense.AnthropologyNote = ToMultiText(fdoSense.AnthroNote);
			lfSense.DiscourseNote = ToMultiText(fdoSense.DiscourseNote);
			lfSense.EncyclopedicNote = ToMultiText(fdoSense.EncyclopedicInfo);
			if (fdoSense.ExamplesOS != null)
				lfSense.Examples = new List<LfExample>(fdoSense.ExamplesOS.Select(FdoExampleToLfExample));
			lfSense.GeneralNote = ToMultiText(fdoSense.GeneralNote);
			lfSense.GrammarNote = ToMultiText(fdoSense.GrammarNote);
			lfSense.LiftId = fdoSense.LIFTid;
			if (fdoSense.MorphoSyntaxAnalysisRA != null)
			{
				IPartOfSpeech secondaryPos = null; // Only used in derivational affixes
				IPartOfSpeech pos = ConvertFdoToMongoPartsOfSpeech.FromMSA(fdoSense.MorphoSyntaxAnalysisRA, out secondaryPos);
				// TODO: Write helper function to take BestVernacularAnalysisAlternative and/or other
				// writing system alternatives, and simplify the process of getting a real string
				// (as opposed to a TsString) from it.
				if (pos == null || pos.Abbreviation == null)
					lfSense.PartOfSpeech = null;
				else
					//lfSense.PartOfSpeech = LfStringField.FromString(ToStringOrNull(pos.Abbreviation.get_String(wsEn)));
					lfSense.PartOfSpeech = LfStringField.FromString(
						_convertGrammaticalCategoryOptionList.LfItemKeyString(pos, _wsEn));
				if (secondaryPos == null || secondaryPos.Abbreviation == null)
					lfSense.SecondaryPartOfSpeech = null;
				else
					lfSense.SecondaryPartOfSpeech = LfStringField.FromString(
						_convertGrammaticalCategoryOptionList.LfItemKeyString(pos, _wsEn));
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
			foreach (LfPicture picture in lfSense.Pictures)
			{
				// TODO: Remove this debugging foreach loop once we know pictures are working
				Logger.Debug("Picture with caption {0} and filename {1}", picture.Caption.FirstNonEmptyString(), picture.FileName);
			}
			lfSense.SenseBibliography = ToMultiText(fdoSense.Bibliography);
			lfSense.SensePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.PublishIn);
			lfSense.SenseRestrictions = ToMultiText(fdoSense.Restrictions);

			if (fdoSense.ReversalEntriesRC != null)
			{
				IEnumerable<string> reversalEntries = fdoSense.ReversalEntriesRC.Select(fdoReversalEntry => fdoReversalEntry.LongName);
				lfSense.ReversalEntries = LfStringArrayField.FromStrings(reversalEntries);
			}
			lfSense.ScientificName = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem.Id, fdoSense.ScientificName);
			lfSense.SemanticDomain = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.SemanticDomainsRC);
			lfSense.SemanticsNote = ToMultiText(fdoSense.SemanticsNote);
			// fdoSense.SensesOS; // Not mapped because LF doesn't handle subsenses. TODO: When LF handles subsenses, map this one.
			lfSense.SenseType = LfStringField.FromString(_convertSenseTypeOptionList.LfItemKeyString(fdoSense.SenseTypeRA, _wsEn));
			lfSense.SociolinguisticsNote = ToMultiText(fdoSense.SocioLinguisticsNote);
			if (fdoSense.Source != null)
			{
				lfSense.Source = LfMultiText.FromSingleITsStringMapping(VernacularWritingSystem.Id, fdoSense.Source);
			}
			lfSense.Status = LfStringArrayField.FromSinglePossibilityAbbrev(fdoSense.StatusRA);
			lfSense.Usages = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.UsageTypesRC);

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

			string lfCustomFieldType;
			LfConfigFieldBase lfCustomFieldSettings;
			BsonDocument customFieldsAndGuids;
			_convertCustomField.getCustomFieldsForThisCmObject(fdoSense, "senses",
				out customFieldsAndGuids, out lfCustomFieldType, out lfCustomFieldSettings);
			Console.WriteLine("lf custom field type {0}", lfCustomFieldType);
			Console.WriteLine("lf custom field settings {0}", lfCustomFieldSettings);

			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			//if (customFieldsBson.Count() > 0)
			//	_lfCustomFieldList.Add(customFieldsBson.Names.First(), new LfConfigMultiText());

			// Role Views only set on initial clone
			if (InitialClone)
			{
				;
			}

			// If custom field was deleted in Flex, delete config here


			lfSense.CustomFields = customFieldsBson;
			lfSense.CustomFieldGuids = customFieldGuids;
			//Logger.Notice("Custom fields for this sense: {0}", lfSense.CustomFields);
			//Logger.Notice("Custom field GUIDs for this sense: {0}", lfSense.CustomFieldGuids);

			return lfSense;
		}

		public LfExample FdoExampleToLfExample(ILexExampleSentence fdoExample)
		{
			LfExample result = new LfExample();

			CoreWritingSystemDefinition VernacularWritingSystem = Cache.LanguageProject.DefaultVernacularWritingSystem;

			result.Guid = fdoExample.Guid;
			result.ExamplePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoExample.PublishIn);
			result.Sentence = ToMultiText(fdoExample.Example);
			result.Reference = LfMultiText.FromSingleITsStringMapping(VernacularWritingSystem.Id, fdoExample.Reference);
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
				result.Translation = ToMultiText(translation.Translation);
				result.TranslationGuid = translation.Guid;
			}

			string lfCustomFieldType;
			LfConfigFieldBase lfCustomFieldSettings;
			BsonDocument customFieldsAndGuids;
			_convertCustomField.getCustomFieldsForThisCmObject(fdoExample, "examples",
				out customFieldsAndGuids, out lfCustomFieldType, out lfCustomFieldSettings);
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			result.CustomFields = customFieldsBson;
			result.CustomFieldGuids = customFieldGuids;
			//Logger.Notice("Custom fields for this example: {0}", result.CustomFields);
			//Logger.Notice("Custom field GUIDs for this example: {0}", result.CustomFieldGuids);
			return result;
		}

		public LfPicture FdoPictureToLfPicture(ICmPicture fdoPicture)
		{
			var result = new LfPicture();
			result.Caption = ToMultiText(fdoPicture.Caption);
			if ((fdoPicture.PictureFileRA != null) && (!string.IsNullOrEmpty(fdoPicture.PictureFileRA.InternalPath)))
			{
				// Remove "Pictures" directory from internal path name
				// If the incoming internal path doesn't begin with "Pictures", then preserve the full external path.
				Regex regex = new Regex(@"^Pictures[/\\]");
				result.FileName = regex.Replace(fdoPicture.PictureFileRA.InternalPath, "");
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

		/// <summary>
		/// Converts FDO writing systems to LF input systems
		/// </summary>
		/// <returns>The list of LF input systems.</returns>
		private Dictionary<string, LfInputSystemRecord> FdoWsToLfWs()
		{
			IList<CoreWritingSystemDefinition> vernacularWSList = Cache.LanguageProject.CurrentVernacularWritingSystems;
			IList<CoreWritingSystemDefinition> analysisWSList = Cache.LanguageProject.CurrentAnalysisWritingSystems;

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
					Tag = fdoWs.LanguageTag,
					VernacularWS = vernacularWSList.Contains(fdoWs),
					AnalysisWS = analysisWSList.Contains(fdoWs)
				};

				lfWsList.Add(fdoWs.LanguageTag, lfWs);
			}
			return lfWsList;
		}

		public ConvertFdoToMongoOptionList ConvertOptionListFromFdo(ILfProject project, string listCode, ICmPossibilityList fdoOptionList)
		{
			LfOptionList lfExistingOptionList = Connection.GetLfOptionListByCode(project, listCode);
			var converter = new ConvertFdoToMongoOptionList(lfExistingOptionList, _wsEn, listCode, Logger);
			LfOptionList lfChangedOptionList = converter.PrepareOptionListUpdate(fdoOptionList);
			Connection.UpdateRecord(project, lfChangedOptionList, listCode);
			return new ConvertFdoToMongoOptionList(lfChangedOptionList, _wsEn, listCode, Logger);
		}
	}
}

