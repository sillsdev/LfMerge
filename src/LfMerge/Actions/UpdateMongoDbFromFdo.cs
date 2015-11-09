// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using LfMerge.FieldWorks;
using LfMerge.LanguageForge.Model;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.Actions
{
	public class UpdateMongoDbFromFdo: Action
	{
		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.UPDATING; }
		}

		protected override void DoRun(ILfProject project)
		{
			FwProject fwProject = project.FieldWorksProject;
			if (fwProject == null)
			{
				Console.WriteLine("Can't find FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			FdoCache cache = fwProject.Cache;
			if (cache == null)
			{
				Console.WriteLine("Can't find cache for FieldWorks project {0}", project.FwProjectCode);
				return;
			}
			IFdoServiceLocator servLoc = cache.ServiceLocator;
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

			string AnalysisWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultAnalWs);
			string VernacularWritingSystem = servLoc.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);

			// Convenience closure
			Func<IMultiAccessorBase, LfMultiText> ToMultiText =
				(fdoMultiString => (fdoMultiString == null) ? null : LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager));

			foreach (ILexEntry fdoEntry in repo.AllInstances())
			{
				if ((fdoEntry) == null) continue;
				var lfEntry = new LfLexEntry();
				List<ILexSense> fdoSenses = fdoEntry.AllSenses ?? new List<ILexSense>(); // TODO: Handle subsenses
				var fdoHeadWord = fdoEntry.HeadWord;
				if (fdoHeadWord != null)
					Console.WriteLine("Entry {0} from FW:", fdoHeadWord.Text);
				else
					Console.WriteLine("Huh... found an entry with no headword. This might fail.");

				// TODO: Figure out if this is the right mapping for lexemes
				IMoForm fdoLexeme = fdoEntry.LexemeFormOA;
				if (fdoLexeme == null)
				{
					string headword = (fdoHeadWord != null) ? fdoHeadWord.Text : "";
					Console.WriteLine("Entry {0} from FW had no lexeme form, using headword instead", headword);
					lfEntry.Lexeme = LfMultiText.FromSingleStringMapping(VernacularWritingSystem, headword);
				}
				else
				{
					lfEntry.Lexeme = ToMultiText(fdoLexeme.Form);
				}

				DebugOut("Lexeme", lfEntry.Lexeme);

				foreach (ILexSense fdoSense in fdoSenses)
				{
					// TODO: Currently not dealing with subsenses...
					var lfSense = new LfSense();

					lfSense.Gloss = ToMultiText(fdoSense.Gloss);
					lfSense.Definition = ToMultiText(fdoSense.Definition);

					DebugOut("Gloss", lfSense.Gloss);
					DebugOut("Definition", lfSense.Definition);

					// TODO: Need more fields here.
					lfSense.SenseBibliography = ToMultiText(fdoSense.Bibliography);
					lfSense.AnthropologyNote = ToMultiText(fdoSense.AnthroNote);
					lfSense.DiscourseNote = ToMultiText(fdoSense.DiscourseNote);
					lfSense.EncyclopedicNote = ToMultiText(fdoSense.EncyclopedicInfo);
					lfSense.GeneralNote = ToMultiText(fdoSense.GeneralNote);

					DebugOut("Sense bibliography", lfSense.SenseBibliography);
					DebugOut("Anthropology note", lfSense.AnthropologyNote);
					DebugOut("Discourse note", lfSense.DiscourseNote);
					DebugOut("Encyclopedic note", lfSense.EncyclopedicNote);
					DebugOut("General note", lfSense.GeneralNote);

					if (lfEntry.Senses == null)
						Console.WriteLine("Oops, lfEntry.Senses shouldn't be null!");
					else
						lfEntry.Senses.Add(lfSense);
				}

				lfEntry.CitationForm = ToMultiText(fdoEntry.CitationForm);
				lfEntry.DateCreated = fdoEntry.DateCreated;
				lfEntry.DateModified = fdoEntry.DateModified;
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

		protected override ActionNames NextActionName
		{
			get { return ActionNames.None; }
		}
	}
}

