// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using LfMerge.DataConverters;
using MongoDB.Bson;
using MongoDB.Driver;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
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

		private FdoCache cache;
		private IFdoServiceLocator servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;
		private CustomFieldConverter converter;
		private IMongoConnection connection;

		public UpdateMongoDbFromFdo(IMongoConnection conn)
		{
			connection = conn;
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
			cache = fwProject.Cache;
			if (cache == null)
			{
				Console.WriteLine("Can't find cache for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			servLoc = cache.ServiceLocator;
			if (servLoc == null)
			{
				Console.WriteLine("Can't find service locator for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			ILexEntryRepository repo = servLoc.GetInstance<ILexEntryRepository>();
			if (repo == null)
			{
				Console.WriteLine("Can't find LexEntry repository for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			if (fdoMetaData == null)
			{
				Console.WriteLine("***WARNING:*** Don't have access to the FW metadata; custom fields may fail!");
			}
			converter = new CustomFieldConverter(cache);

			IMongoDatabase mongoDb = connection.GetProjectDatabase(project);
			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				LfLexEntry lfEntry = FdoLexEntryToLfLexEntry(fdoEntry);
				Console.WriteLine("Populated LfEntry {0}", lfEntry.Guid);
				// TODO: Write the lfEntry into Mongo in the right place
				// TODO: Move this "update this document in this MongoDB collection" code to somewhere where it belongs, like on MongoConnection
				var filterBuilder = new FilterDefinitionBuilder<LfLexEntry>();
				var fb = Builders<LfLexEntry>.Filter;
				var update = connection.BuildUpdate<LfLexEntry>(lfEntry);
				var filter = filterBuilder.Eq("guid", lfEntry.Guid.ToString());
				var collection = mongoDb.GetCollection<LfLexEntry>("lexicon");
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
			return LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
		}

		private string ToStringOrNull(ITsString iTsString)
		{
			if (iTsString == null) return null;
			return iTsString.Text;
		}

		private LfSense FdoSenseToLfSense(ILexSense fdoSense)
		{
			var lfSense = new LfSense();

			string VernacularWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);
			string AnalysisWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultAnalWs);

			// TODO: Currently skipping subsenses. Figure out if we should include them or not.

			lfSense.SenseId = fdoSense.Id.ToString(); // TODO: Is this right? This doesn't quite feel right.

			lfSense.Gloss = ToMultiText(fdoSense.Gloss);
			lfSense.Definition = ToMultiText(fdoSense.Definition);

			DebugOut("Gloss", lfSense.Gloss);
			DebugOut("Definition", lfSense.Definition);

			// Fields below in alphabetical order by ILexSense property, except for Gloss and Definition
			lfSense.AnthropologyCategories = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.AnthroCodesRC);
			lfSense.AnthropologyNote = ToMultiText(fdoSense.AnthroNote);
			lfSense.SenseBibliography = ToMultiText(fdoSense.Bibliography);
			lfSense.AcademicDomains = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.DomainTypesRC);
			lfSense.DiscourseNote = ToMultiText(fdoSense.DiscourseNote);
			lfSense.EncyclopedicNote = ToMultiText(fdoSense.EncyclopedicInfo);
			if (fdoSense.ExamplesOS != null)
				lfSense.Examples = new List<LfExample>(fdoSense.ExamplesOS.Select(example => FdoExampleToLfExample(example)));
			lfSense.GeneralNote = ToMultiText(fdoSense.GeneralNote);
			lfSense.GrammarNote = ToMultiText(fdoSense.GrammarNote);
			lfSense.LiftId = fdoSense.LIFTid;
			lfSense.PhonologyNote = ToMultiText(fdoSense.PhonologyNote);
			if (fdoSense.PicturesOS != null)
				lfSense.Pictures = new List<LfPicture>(fdoSense.PicturesOS.Select(picture => FdoPictureToLfPicture(picture)));
			foreach (LfPicture picture in lfSense.Pictures)
			{
				// TODO: Remove this debugging foreach loop
				Console.WriteLine("Picture with caption {0} and filename {1}", picture.Caption, picture.FileName);
			}
			lfSense.SensePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.PublishIn);
			lfSense.SenseRestrictions = ToMultiText(fdoSense.Restrictions);

			if (fdoSense.ReversalEntriesRC != null)
			{
				var reversalEntries = new List<string>();
				foreach (IReversalIndexEntry fdoReversalEntry in fdoSense.ReversalEntriesRC)
				{
					reversalEntries.Add(fdoReversalEntry.LongName);
				}
				lfSense.ReversalEntries = LfStringArrayField.FromStrings(reversalEntries);
			}
			lfSense.ScientificName = LfMultiText.FromSingleITsStringMapping(AnalysisWritingSystem, fdoSense.ScientificName);
			lfSense.SemanticDomain = LfStringArrayField.FromPossibilityAbbrevs(fdoSense.SemanticDomainsRC);

			DebugOut("Semantic domains", lfSense.SemanticDomain);

			lfSense.SemanticsNote = ToMultiText(fdoSense.SemanticsNote);
			// fdoSense.SensesOS; // Not mapped because LF doesn't handle subsenses. TODO: When LF handles subsenses, map this one.
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
			fdoSense.Guid; // Using LIFTid instead. TODO: Verify whether that's correct.
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
			fdoSense.SenseTypeRA;
			fdoSense.Subentries;
			fdoSense.ThesaurusItemsRC;
			fdoSense.MorphoSyntaxAnalysisRA;
			fdoSense.LiftResidue;
			fdoSense.LexSenseOutline;
			*/

			DebugOut("Sense bibliography", lfSense.SenseBibliography);
			DebugOut("Anthropology note", lfSense.AnthropologyNote);
			DebugOut("Discourse note", lfSense.DiscourseNote);
			DebugOut("Encyclopedic note", lfSense.EncyclopedicNote);
			DebugOut("General note", lfSense.GeneralNote);
			DebugOut("Usages", lfSense.Usages);

			BsonDocument customFieldsAndGuids = converter.CustomFieldsForThisCmObject(fdoSense, "senses");
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

			string VernacularWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);

			result.ExamplePublishIn = LfStringArrayField.FromPossibilityAbbrevs(fdoExample.PublishIn);
			result.Sentence = ToMultiText(fdoExample.Example);
			result.Reference = LfMultiText.FromSingleITsStringMapping(VernacularWritingSystem, fdoExample.Reference);
			// ILexExampleSentence fields we currently do not convert:
			// fdoExample.DoNotPublishInRC;
			// fdoExample.LiftResidue;
			// fdoExample.TranslationsOC;

			BsonDocument customFieldsAndGuids = converter.CustomFieldsForThisCmObject(fdoExample, "examples");
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
			if ((fdoEntry) == null) return null;
			Console.WriteLine("Converting one entry");

			string AnalysisWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultAnalWs);
			// string VernacularWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);

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
				lfEntry.MorphologyType = fdoEntry.PrimaryMorphType.Name.BestAnalysisVernacularAlternative.Text; // TODO: What if there are nulls in that long string of property accessors?
				Console.WriteLine("Morphology type for {0} was {1}", fdoEntry.Guid, lfEntry.MorphologyType);
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
			lfEntry.Senses.AddRange(fdoEntry.SensesOS.Select(fdoSense => FdoSenseToLfSense(fdoSense)));
			lfEntry.SummaryDefinition = ToMultiText(fdoEntry.SummaryDefinition);

			DebugOut("Citation form", lfEntry.CitationForm);
			DebugOut("Entry restrictions", lfEntry.EntryRestrictions);
			DebugOut("Etymology Source", lfEntry.EtymologySource);

			BsonDocument customFieldsAndGuids = converter.CustomFieldsForThisCmObject(fdoEntry, "entry");
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
