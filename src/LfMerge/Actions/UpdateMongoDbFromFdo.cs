﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.DataConverters;
using LfMerge.MongoConnector;
using MongoDB.Bson;
using MongoDB.Driver;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.Actions
{
	public class UpdateMongoDbFromFdo: Action
	{
		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.UPDATING; }
		}

		private FdoCache _cache;
		private IFdoServiceLocator _servLoc;
		private IFwMetaDataCacheManaged _fdoMetaData;
		private CustomFieldConverter _converter;
		private IMongoConnection _connection;

		public UpdateMongoDbFromFdo(ILfMergeSettings settings, IMongoConnection conn) : base(settings)
		{
			_connection = conn;
		}

		//private List<int> customFieldIds;

		protected override void DoRun(ILfProject project)
		{
			FwProject fwProject = project.FieldWorksProject;
			if (fwProject == null)
			{
				Console.WriteLine("Can't find FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			_cache = fwProject.Cache;
			if (_cache == null)
			{
				Console.WriteLine("Can't find cache for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			_servLoc = _cache.ServiceLocator;
			if (_servLoc == null)
			{
				Console.WriteLine("Can't find service locator for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			ILexEntryRepository repo = _servLoc.GetInstance<ILexEntryRepository>();
			if (repo == null)
			{
				Console.WriteLine("Can't find LexEntry repository for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			_fdoMetaData = (IFwMetaDataCacheManaged)_cache.MetaDataCacheAccessor;
			if (_fdoMetaData == null)
			{
				Console.WriteLine("***WARNING:*** Don't have access to the FW metadata; custom fields may fail!");
			}
			_converter = new CustomFieldConverter(_cache);

			IMongoDatabase mongoDb = _connection.GetProjectDatabase(project);
			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				LfLexEntry lfEntry = FdoLexEntryToLfLexEntry(fdoEntry);
				Console.WriteLine("Populated LfEntry {0}", lfEntry.Guid);
				// TODO: Write the lfEntry into Mongo in the right place
				// TODO: Move this "update this document in this MongoDB collection" code to somewhere where it belongs, like on MongoConnection
				var filterBuilder = new FilterDefinitionBuilder<LfLexEntry>();
				UpdateDefinition<LfLexEntry> update = _connection.BuildUpdate<LfLexEntry>(lfEntry);
				FilterDefinition<LfLexEntry> filter = filterBuilder.Eq("guid", lfEntry.Guid.ToString());
				IMongoCollection<LfLexEntry> collection = mongoDb.GetCollection<LfLexEntry>("lexicon");
				Console.WriteLine("About to save LfEntry {0} which has morphologyType {1}", lfEntry.Guid, lfEntry.MorphologyType);
				//var result = collection.FindOneAndReplaceAsync(filter, lfEntry).Result;
				Console.WriteLine("Built filter that looks like: {0}", filter.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
				Console.WriteLine("Built update that looks like: {0}", update.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry).ToJson());
				//var ignored = collection.FindOneAndUpdateAsync(filter, update).Result; // NOTE: Throwing away result on purpose.
				Console.WriteLine("Done saving LfEntry {0} into Mongo DB {1}", lfEntry.Guid, mongoDb.DatabaseNamespace.DatabaseName);
			}
		}

		private void DebugOut(string fieldName, LfMultiText multiText)
		{
			if (multiText == null) return;
			foreach (KeyValuePair<string, LfStringField> kv in multiText)
			{
				Console.WriteLine("{0} in writing system {1} is \"{2}\"", fieldName, kv.Key, kv.Value);
			}
		}

		private void DebugOut(string fieldName, LfStringArrayField strings)
		{
			Console.WriteLine("{0} values are [{1}]", fieldName, String.Join(", ", strings.Values));
		}

		private LfMultiText ToMultiText(IMultiAccessorBase fdoMultiString)
		{
			if (fdoMultiString == null) return null;
			return LfMultiText.FromFdoMultiString(fdoMultiString, _servLoc.WritingSystemManager);
		}

		private string ToStringOrNull(ITsString iTsString)
		{
			if (iTsString == null) return null;
			return iTsString.Text;
		}

		private LfSense FdoSenseToLfSense(ILexSense fdoSense)
		{
			var lfSense = new LfSense();

			string VernacularWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultVernWs);
			string AnalysisWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultAnalWs);

			// TODO: Currently skipping subsenses. Figure out if we should include them or not.

			lfSense.Guid = fdoSense.Guid;
			lfSense.Gloss = ToMultiText(fdoSense.Gloss);
			lfSense.Definition = ToMultiText(fdoSense.Definition);

			DebugOut("Gloss", lfSense.Gloss);
			DebugOut("Definition", lfSense.Definition);

			// Fields below in alphabetical order by ILexSense property, except for Guid, Gloss and Definition
			lfSense.AnthropologyCategories = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.AnthroCodesRC);
			lfSense.AnthropologyNote = ToMultiText(fdoSense.AnthroNote);
			lfSense.SenseBibliography = ToMultiText(fdoSense.Bibliography);
			lfSense.AcademicDomains = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.DomainTypesRC);
			lfSense.DiscourseNote = ToMultiText(fdoSense.DiscourseNote);
			lfSense.EncyclopedicNote = ToMultiText(fdoSense.EncyclopedicInfo);
			if (fdoSense.ExamplesOS != null)
				lfSense.Examples = new List<LfExample>(fdoSense.ExamplesOS.Select(FdoExampleToLfExample));
			lfSense.GeneralNote = ToMultiText(fdoSense.GeneralNote);
			lfSense.GrammarNote = ToMultiText(fdoSense.GrammarNote);
			lfSense.LiftId = fdoSense.LIFTid;
			if (fdoSense.MorphoSyntaxAnalysisRA != null)
			{
				IPartOfSpeech pos = PartOfSpeechConverter.FromMSA(fdoSense.MorphoSyntaxAnalysisRA);
				if (pos == null)
					lfSense.PartOfSpeech = null;
				else
					lfSense.PartOfSpeech = LfStringField.FromString(pos.NameHierarchyString);
					// Or: lfSense.PartOfSpeech = LfStringField.FromString(pos.Name.BestAnalysisVernacularAlternative.Text);
				// TODO: Should we add a PartOfSpeech GUID here? Or the GUID of the MSA? Think about it.
			}
			lfSense.PhonologyNote = ToMultiText(fdoSense.PhonologyNote);
			if (fdoSense.PicturesOS != null)
				lfSense.Pictures = new List<LfPicture>(fdoSense.PicturesOS.Select(FdoPictureToLfPicture));
			foreach (LfPicture picture in lfSense.Pictures)
			{
				// TODO: Remove this debugging foreach loop
				Console.WriteLine("Picture with caption {0} and filename {1}", picture.Caption, picture.FileName);
			}
			lfSense.SensePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.PublishIn);
			lfSense.SenseRestrictions = ToMultiText(fdoSense.Restrictions);

			if (fdoSense.ReversalEntriesRC != null)
			{
				IEnumerable<string> reversalEntries = fdoSense.ReversalEntriesRC.Select(fdoReversalEntry => fdoReversalEntry.LongName);
				lfSense.ReversalEntries = LfStringArrayField.FromStrings(reversalEntries);
			}
			lfSense.ScientificName = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem, fdoSense.ScientificName);
			lfSense.SemanticDomain = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.SemanticDomainsRC);

			DebugOut("Semantic domains", lfSense.SemanticDomain);

			lfSense.SemanticsNote = ToMultiText(fdoSense.SemanticsNote);
			// fdoSense.SensesOS; // Not mapped because LF doesn't handle subsenses. TODO: When LF handles subsenses, map this one.
			if (fdoSense.SenseTypeRA != null)
				lfSense.SenseType = LfStringField.FromString(fdoSense.SenseTypeRA.NameHierarchyString);
			lfSense.SociolinguisticsNote = ToMultiText(fdoSense.SocioLinguisticsNote);
			if (fdoSense.Source != null)
			{
				lfSense.Source = LfMultiText.FromSingleITsStringMapping(VernacularWritingSystem, fdoSense.Source);
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

			DebugOut("Sense bibliography", lfSense.SenseBibliography);
			DebugOut("Anthropology note", lfSense.AnthropologyNote);
			DebugOut("Discourse note", lfSense.DiscourseNote);
			DebugOut("Encyclopedic note", lfSense.EncyclopedicNote);
			DebugOut("General note", lfSense.GeneralNote);
			DebugOut("Usages", lfSense.Usages);

			BsonDocument customFieldsAndGuids = _converter.CustomFieldsForThisCmObject(fdoSense, "senses");
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfSense.CustomFields = customFieldsBson;
			lfSense.CustomFieldGuids = customFieldGuids;
			Console.WriteLine("Custom fields for this sense:");
			Console.WriteLine(lfSense.CustomFields);
			Console.WriteLine("Custom field GUIDs for this sense:");
			Console.WriteLine(lfSense.CustomFieldGuids);

			return lfSense;
		}

		private LfExample FdoExampleToLfExample(ILexExampleSentence fdoExample)
		{
			LfExample result = new LfExample();

			string VernacularWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultVernWs);

			result.ExamplePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoExample.PublishIn);
			result.Sentence = ToMultiText(fdoExample.Example);
			result.Reference = LfMultiText.FromSingleITsStringMapping(VernacularWritingSystem, fdoExample.Reference);
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

			BsonDocument customFieldsAndGuids = _converter.CustomFieldsForThisCmObject(fdoExample, "examples");
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			result.CustomFields = customFieldsBson;
			result.CustomFieldGuids = customFieldGuids;
			Console.WriteLine("Custom fields for this example:");
			Console.WriteLine(result.CustomFields);
			Console.WriteLine("Custom field GUIDs for this example:");
			Console.WriteLine(result.CustomFieldGuids);

			return result;
		}

		private LfPicture FdoPictureToLfPicture(ICmPicture fdoPicture)
		{
			var result = new LfPicture();
			result.Caption = ToMultiText(fdoPicture.Caption);
			if (fdoPicture.PictureFileRA != null)
				result.FileName = fdoPicture.PictureFileRA.InternalPath;
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

		private LfLexEntry FdoLexEntryToLfLexEntry(ILexEntry fdoEntry)
		{
			if (fdoEntry == null) return null;
			Console.WriteLine("Converting one entry");

			string AnalysisWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultAnalWs);
			// string VernacularWritingSystem = _servLoc.WritingSystemManager.GetStrFromWs(_cache.DefaultVernWs);

			var lfEntry = new LfLexEntry();

			IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
			if (fdoLexeme == null)
				lfEntry.Lexeme = null;
			else
				lfEntry.Lexeme = ToMultiText(fdoLexeme.Form);
			// Other fields of fdoLexeme (AllomorphEnvironments, LiftResidue, MorphTypeRA, etc.) not mapped

			DebugOut("Lexeme", lfEntry.Lexeme);

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
				lfEntry.EtymologySource = LfMultiText.FromSingleStringMapping(AnalysisWritingSystem, fdoEtymology.Source);
				// fdoEtymology.LiftResidue not mapped
			}
			lfEntry.Guid = fdoEntry.Guid;
			lfEntry.LiftId = fdoEntry.LIFTid;
			lfEntry.LiteralMeaning = ToMultiText(fdoEntry.LiteralMeaning);
			if (fdoEntry.PrimaryMorphType != null) {
				lfEntry.MorphologyType = fdoEntry.PrimaryMorphType.NameHierarchyString;
			}
			// TODO: Once LF's data model is updated from a single pronunciation to an array of pronunciations, convert all of the. E.g.,
			//foreach (ILexPronunciation fdoPronunciation in fdoEntry.PronunciationsOS) { ... }
			if (fdoEntry.PronunciationsOS.Count > 0)
			{
				ILexPronunciation fdoPronunciation = fdoEntry.PronunciationsOS.First();
				lfEntry.PronunciationGuid = fdoPronunciation.Guid;
				lfEntry.Pronunciation = ToMultiText(fdoPronunciation.Form);
				lfEntry.CvPattern = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem, fdoPronunciation.CVPattern);
				lfEntry.Tone = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem, fdoPronunciation.Tone);
				// TODO: Map fdoPronunciation.MediaFilesOS properly (converting video to sound files if necessary)
				//lfEntry.Location = LfStringField.FromString(fdoPronunciation.LocationRA.AbbrAndName);
				lfEntry.Location = LfStringField.FromString(PossibilityListConverter.BestStringFrom(fdoPronunciation.LocationRA));
			}
			lfEntry.EntryRestrictions = ToMultiText(fdoEntry.Restrictions);
			if (lfEntry.Senses == null) // Shouldn't happen, but let's be careful
				lfEntry.Senses = new List<LfSense>();
			lfEntry.Senses.AddRange(fdoEntry.SensesOS.Select(FdoSenseToLfSense));
			lfEntry.SummaryDefinition = ToMultiText(fdoEntry.SummaryDefinition);

			DebugOut("Citation form", lfEntry.CitationForm);
			DebugOut("Entry restrictions", lfEntry.EntryRestrictions);
			DebugOut("Etymology Source", lfEntry.EtymologySource);

			BsonDocument customFieldsAndGuids = _converter.CustomFieldsForThisCmObject(fdoEntry, "entry");
			BsonDocument customFieldsBson = customFieldsAndGuids["customFields"].AsBsonDocument;
			BsonDocument customFieldGuids = customFieldsAndGuids["customFieldGuids"].AsBsonDocument;

			lfEntry.CustomFields = customFieldsBson;
			lfEntry.CustomFieldGuids = customFieldGuids;
			Console.WriteLine("Custom fields for this entry:");
			Console.WriteLine(lfEntry.CustomFields);
			Console.WriteLine("Custom field GUIDs for this entry:");
			Console.WriteLine(lfEntry.CustomFieldGuids);

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

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}