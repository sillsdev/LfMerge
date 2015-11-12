// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using MongoDB.Bson;
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

			//customFieldIds = new List<int>(((IFwMetaDataCacheManaged)fdoMetaData).GetFieldIds().Where((flid => cache.GetIsCustomField(flid))));
			/*
			foreach (int flid in customFieldIds)
			{
				if (cache.GetIsCustomField(flid))
				{
					Console.WriteLine("Custom field with ID {0}", flid);
					Console.WriteLine("  Field name: {0}", fdoMetaData.GetFieldName(flid));
					Console.WriteLine("  Field label: {0}", fdoMetaData.GetFieldLabel(flid));
					Console.WriteLine("  Field destination: {0}", fdoMetaData.GetDstClsName(flid));
					//Console.WriteLine("  Field base class: {0}", fdoMetaData.GetBaseClsName(flid)); // Can't do this for custom fields
					Console.WriteLine("  Field XML: {0}", fdoMetaData.GetFieldXml(flid));
					Console.WriteLine("  Field type (int): {0}", fdoMetaData.GetFieldType(flid));
					CellarPropertyType prop = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
					if (cache.IsReferenceProperty(flid))
						Console.WriteLine("  Field is a reference.");

					if (cache.IsVectorProperty(flid))
						Console.WriteLine("  Field is a vector.");

					Console.WriteLine("  Field type (str): {0}", prop);
					var fieldInfo = new List<ClassAndPropInfo>();
					cache.AddClassesForField(flid, false, fieldInfo);
					foreach (ClassAndPropInfo info in fieldInfo)
					{
						Console.WriteLine("  Signature class: {0}", info.signatureClassName);
						if ((CellarPropertyType)info.fieldType == CellarPropertyType.ReferenceAtom)
							Console.WriteLine("Reference type!");
					}
				}
			}
			Console.WriteLine("Exiting for debugging...");
			return; // Deliberately stop here
			*/
//			// Convenience closure
//			Func<IMultiAccessorBase, LfMultiText> ToMultiText =
//				(fdoMultiString => (fdoMultiString == null) ? null : LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager));

			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				LfLexEntry lfEntry = FdoLexEntryToLfLexEntry(fdoEntry);
				Console.WriteLine("Populated LfEntry {0}", lfEntry.Guid);
				// TODO: Write the lfEntry into Mongo in the right place
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
			fdoSense.Id;
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

			BsonDocument customFieldsBson = converter.CustomFieldsForThisCmObject(fdoSense);

			lfSense.CustomFields = customFieldsBson;
			Console.WriteLine("Custom fields for this sense:");
			Console.WriteLine(lfSense.CustomFields);

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

			BsonDocument customFieldsBsonForExample = converter.CustomFieldsForThisCmObject(fdoExample);

			result.CustomFields = customFieldsBsonForExample;
			Console.WriteLine("Custom fields for this example:");
			Console.WriteLine(result.CustomFields);

			return result;
		}

		private LfPicture FdoPictureToLfPicture(ICmPicture fdoPicture)
		{
			var result = new LfPicture();
			result.Caption = ToMultiText(fdoPicture.Caption);
			result.FileName = fdoPicture.PictureFileRA.InternalPath;
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
			string VernacularWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);

			var lfEntry = new LfLexEntry();

			var fdoHeadWord = ToStringOrNull(fdoEntry.HeadWord);
			if (fdoHeadWord != null)
				Console.WriteLine("Entry {0} from FW:", fdoHeadWord);
			else
				Console.WriteLine("Huh... found an entry with no headword. This might fail.");

			// TODO: Figure out if this is the right mapping for lexemes
			IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
			if (fdoLexeme == null)
			{
				string headword = fdoHeadWord ?? "";
				Console.WriteLine("Entry {0} from FW had no lexeme form, using headword instead", headword);
				lfEntry.Lexeme = LfMultiText.FromSingleStringMapping(VernacularWritingSystem, headword);
			}
			else
			{
				lfEntry.Lexeme = ToMultiText(fdoLexeme.Form);
			}

			DebugOut("Lexeme", lfEntry.Lexeme);

			foreach (ILexSense fdoSense in fdoEntry.SensesOS)
			{
				LfSense lfSense = FdoSenseToLfSense(fdoSense);

				if (lfEntry.Senses == null)
					Console.WriteLine("Oops, lfEntry.Senses shouldn't be null!");
				else
					lfEntry.Senses.Add(lfSense);
			}

			lfEntry.CitationForm = ToMultiText(fdoEntry.CitationForm);

			lfEntry.DateCreated = fdoEntry.DateCreated;
			lfEntry.DateModified = fdoEntry.DateModified;

			// TODO: Pretty sure this block is wrong. AuthorInfo.CreatedDate in Mongo doesn't match DateCreated. One of them is for something else.
			// TODO: Figure out which one is for what.
			if (lfEntry.AuthorInfo == null)
				lfEntry.AuthorInfo = new LfAuthorInfo();
			lfEntry.AuthorInfo.CreatedByUserRef = null;
			lfEntry.AuthorInfo.CreatedDate = fdoEntry.DateCreated;
			lfEntry.AuthorInfo.ModifiedByUserRef = null;
			lfEntry.AuthorInfo.ModifiedDate = fdoEntry.DateModified;

			lfEntry.EntryBibliography = ToMultiText(fdoEntry.Bibliography);
			lfEntry.EntryRestrictions = ToMultiText(fdoEntry.Restrictions);
			ILexEtymology fdoEtymology = fdoEntry.EtymologyOA;
			if (fdoEtymology != null)
			{
				lfEntry.Etymology = ToMultiText(fdoEtymology.Form); // TODO: Check if ILexEtymology.Form is the right field here
				lfEntry.EtymologyComment = ToMultiText(fdoEtymology.Comment);
				lfEntry.EtymologyGloss = ToMultiText(fdoEtymology.Gloss);
				lfEntry.EtymologySource = LfMultiText.FromSingleStringMapping(AnalysisWritingSystem, fdoEtymology.Source);
			}

			DebugOut("Citation form", lfEntry.CitationForm);
			DebugOut("Entry restrictions", lfEntry.EntryRestrictions);
			DebugOut("Etymology Source", lfEntry.EtymologySource);

			foreach (IMoForm allomorph in fdoEntry.AlternateFormsOS)
			{
				// Do nothing; LanguageForge doesn't handle allomorphs, so we don't convert them
			}

			BsonDocument customFieldsBson = converter.CustomFieldsForThisCmObject(fdoEntry);

			lfEntry.CustomFields = customFieldsBson;
			Console.WriteLine("Custom fields for this entry:");
			Console.WriteLine(lfEntry.CustomFields);

			return lfEntry;
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

/* Fields from an fdoEntry:
				fdoEntry.AllAllomorphs;
				fdoEntry.AllOwnedObjects;
				fdoEntry.AllReferencedObjects(collector);
				fdoEntry.AllSenses;
				fdoEntry.AlternateFormsOS;
				fdoEntry.Bibliography;
				fdoEntry.Cache;
				fdoEntry.CanDelete;
				fdoEntry.ChooserNameTS;
				fdoEntry.CitationForm;
				fdoEntry.CitationFormWithAffixType;
				fdoEntry.ClassID;
				fdoEntry.ClassName;
				fdoEntry.Comment;
				fdoEntry.ComplexFormEntries;
				fdoEntry.ComplexFormEntryRefs;
				fdoEntry.ComplexFormsNotSubentries;
				fdoEntry.DateCreated;
				fdoEntry.DateModified;
				fdoEntry.DeletionTextTSS;
				fdoEntry.DoNotPublishInRC;
				fdoEntry.DoNotShowMainEntryInRC;
				fdoEntry.DoNotUseForParsing;
				fdoEntry.EntryRefsOS;
				fdoEntry.EtymologyOA;
				fdoEntry.GetDefaultClassForNewAllomorph();
				fdoEntry.Guid;
				fdoEntry.HasMoreThanOneSense;
				fdoEntry.HeadWord;
				fdoEntry.HeadWordForWs(wsVern);
				fdoEntry.HeadWordRefForWs(wsVern);
				fdoEntry.HeadWordReversalForWs(wsVern);
				fdoEntry.HomographForm;
				fdoEntry.HomographFormKey;
				fdoEntry.HomographNumber;
				fdoEntry.Hvo;
				fdoEntry.Id;
				fdoEntry.ImportResidue;
				fdoEntry.IndexInOwner;
				fdoEntry.IsCircumfix;
				fdoEntry.IsComponent;
				fdoEntry.IsFieldRelevant;
				fdoEntry.IsFieldRequired;
				fdoEntry.IsMorphTypesMixed;
				fdoEntry.IsOwnedBy(possibleOwner);;
				fdoEntry.IsValidObject;
				fdoEntry.IsVariantOfSenseOrOwnerEntry(senseTargetComponent, out matchinEntryRef);
				fdoEntry.LexemeFormOA;
				fdoEntry.LexEntryReferences;
				fdoEntry.LIFTid;
				fdoEntry.LiftResidue;
				fdoEntry.LiteralMeaning;
				fdoEntry.MainEntriesOrSensesRS;
				fdoEntry.MinimalLexReferences;
				fdoEntry.MorphoSyntaxAnalysesOC;
				fdoEntry.MorphTypes;
				fdoEntry.NumberOfSensesForEntry;
				fdoEntry.ObjectIdName;
				fdoEntry.OwnedObjects;
				fdoEntry.Owner;
				fdoEntry.OwningFlid;
				fdoEntry.OwnOrd;
				fdoEntry.PicturesOfSenses;
				fdoEntry.PrimaryMorphType;
				fdoEntry.PronunciationsOS;
				fdoEntry.PublishAsMinorEntry;
				fdoEntry.PublishIn;
				fdoEntry.ReferenceTargetCandidates(flid);
				fdoEntry.ReferenceTargetOwner(flid);
				fdoEntry.ReferringObjects;
				fdoEntry.Restrictions;
				fdoEntry.Self;
				fdoEntry.SensesOS;
				fdoEntry.SenseWithMsa(MoMorphSynAnalysis);
				fdoEntry.Services;
				fdoEntry.ShortName;
				fdoEntry.ShortNameTSS;
				fdoEntry.ShowMainEntryIn;
				fdoEntry.SortKey;
				fdoEntry.SortKey2;
				fdoEntry.SortKey2Alpha;
				fdoEntry.SortKeyWs;
				fdoEntry.Subentries;
				fdoEntry.SummaryDefinition;
				fdoEntry.SupportsInflectionClasses();
				fdoEntry.VariantEntryRefs;
				fdoEntry.VariantFormEntries;
				fdoEntry.VariantFormEntryBackRefs;
				fdoEntry.VisibleComplexFormBackRefs;
				fdoEntry.VisibleComplexFormEntries;
				fdoEntry.VisibleVariantEntryRefs;
*/



/* Fields from an fdoSense:
fdoSense.AllOwnedObjects;
fdoSense.AllSenses;
fdoSense.AnthroCodesRC;
fdoSense.AnthroNote;
fdoSense.AppendixesRC;
fdoSense.Bibliography;
fdoSense.Cache;
fdoSense.CanDelete;
fdoSense.ChooserNameTS;
fdoSense.ClassID;
fdoSense.ClassName;
fdoSense.ComplexFormEntries;
fdoSense.ComplexFormsNotSubentries;
fdoSense.Definition;
fdoSense.DefinitionOrGloss;
fdoSense.DeletionTextTSS;
fdoSense.DiscourseNote;
fdoSense.DomainTypesRC;
fdoSense.DoNotPublishInRC;
fdoSense.EncyclopedicInfo;
fdoSense.Entry;
fdoSense.EntryID;
fdoSense.ExamplesOS;
fdoSense.FullReferenceName;
fdoSense.GeneralNote;
fdoSense.GetDesiredMsaType();
fdoSense.Gloss;
fdoSense.GrammarNote;
fdoSense.Guid;
fdoSense.Hvo;
fdoSense.Id;
fdoSense.ImportResidue;
fdoSense.IndexInOwner;
fdoSense.IsValidObject;
fdoSense.LexSenseOutline;
fdoSense.LexSenseReferences;
fdoSense.LIFTid;
fdoSense.LiftResidue;
fdoSense.LongNameTSS;
fdoSense.MorphoSyntaxAnalysisRA;
fdoSense.ObjectIdName;
fdoSense.OwnedObjects;
fdoSense.Owner;
fdoSense.OwningFlid;
fdoSense.OwnOrd;
fdoSense.PhonologyNote;
fdoSense.PicturesOS;
fdoSense.PublishIn;
fdoSense.ReferringObjects;
fdoSense.Restrictions;
fdoSense.ReversalEntriesRC;
fdoSense.ReversalNameForWs(wsVern);
// fdoSense.SandboxMSA; // Set-only property
fdoSense.ScientificName;
fdoSense.Self;
fdoSense.SemanticDomainsRC;
fdoSense.SemanticsNote;
fdoSense.SensesOS;
fdoSense.SenseTypeRA;
fdoSense.Services;
fdoSense.ShortName;
fdoSense.ShortNameTSS;
fdoSense.SocioLinguisticsNote;
fdoSense.SortKey;
fdoSense.SortKey2;
fdoSense.SortKey2Alpha;
fdoSense.SortKeyWs;
fdoSense.Source;
fdoSense.StatusRA;
fdoSense.Subentries;
fdoSense.ThesaurusItemsRC;
fdoSense.UsageTypesRC;
fdoSense.VariantFormEntryBackRefs;
fdoSense.VisibleComplexFormBackRefs;
*/
