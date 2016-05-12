// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;

namespace LfMerge.DataConverters.CanonicalSources
{
	public struct SemDomQuestion
	{
		public string Question;
		public string ExampleWords;      // Might be null if none found
		public string ExampleSentences;  // Might be null if none found
	}

	public class CanonicalSemanticDomainItem : CanonicalItem
	{
		// Get an <AUni ws="en"> type value
		// or an <AStr ws="en"><Run ws="en"> type value as well.
		// Will never return null. Returns an empty string if no text found.
		private string GetStr(XmlReader reader, out string ws)
		{
			ws = string.Empty;
			string result = string.Empty;
			string name = reader.LocalName;
			while (reader.Read())
			{
				switch (reader.NodeType)
				{
					case XmlNodeType.Element:
						{
							switch (reader.LocalName)
							{
								case "AUni":
								case "Run":
									ws = reader.GetAttribute("ws");
									result = reader.ReadInnerXml();
									break;
								case "Uni":
									result = reader.ReadInnerXml();
									break;
							}
						}
						break;
					case XmlNodeType.EndElement:
						if (reader.LocalName == name)
						{
							// Advance past closing element before returning
							reader.Read();
							return result ?? string.Empty;
						}
						break;
				}
			}
			return result ?? string.Empty;
		}

		private SemDomQuestion GetQuestion(XmlReader reader, out string ws)
		{
			ws = string.Empty;
			SemDomQuestion result = new SemDomQuestion();
			if (reader.LocalName != "CmDomainQ")
				return result;  // If we weren't on the right kind of node, return empty SemDomQuestion
			string name = reader.LocalName;
			while (reader.Read())
			{
				while (reader.Read())
				{
					switch (reader.NodeType)
					{
						case XmlNodeType.Element:
							{
								switch (reader.LocalName)
								{
									case "Question":
										result.Question = GetStr(reader, out ws);
										break;
									case "ExampleWords":
										result.ExampleWords = GetStr(reader, out ws);
										break;
									case "ExampleSentences":
										result.ExampleSentences = GetStr(reader, out ws);
										break;
								}
							}
							break;
						case XmlNodeType.EndElement:
							if (reader.LocalName == "CmDomainQ")
							{
								// Advance past closing element before returning
								reader.Read();
								return result;
							}
							break;
					}
				}
			}
			return result;
		}

		public override void PopulateFromXml(XmlReader reader)
		{
			if (reader.LocalName != "CmSemanticDomain")
				return;  // If we weren't on the right kind of node, do nothing
			GuidStr = reader.GetAttribute("guid");
			string ws = string.Empty; // Used as the out param in GetStr()
			// Note that if the writing systems in the SemDom.xml file are inconsistent, then
			// using a single out parameter everywhere may create inconsistent results. If we
			// ever need to parse a SemDom.xml with multiple writing systems in it, we might
			// have to change how we handle the "out ws" parameters in this function.
			while (reader.Read())
			{
				switch (reader.NodeType)
				{
					case XmlNodeType.Element:
						{
							switch (reader.LocalName)
							{
								case "CmSemanticDomain":
									var child = new CanonicalSemanticDomainItem();
									child.PopulateFromXml(reader);
									AppendChild(child);
									break;
								case "Abbreviation":
									string abbrev = GetStr(reader, out ws);
									AddAbbrev(ws, abbrev);
									break;
								case "Name":
									string name = GetStr(reader, out ws);
									AddName(ws, name);
									break;
								case "Description":
									string desc = GetStr(reader, out ws);
									AddDescription(ws, desc);
									break;
								case "OcmCodes":
									List<string> ocmCodes = GetExtraDataList<string>("OcmCodes");
									string ocmCodesText = GetStr(reader, out ws);
									ocmCodes.AddRange(ocmCodesText.Split(new string[] { ";  " }, StringSplitOptions.None));
									break;
								case "LouwNidaCodes":
									List<string> louwNidaCodes = GetExtraDataList<string>("OcmCodes");
									string louwNidaCodesText = GetStr(reader, out ws);
									louwNidaCodes.AddRange(louwNidaCodesText.Split(new string[] { ";  " }, StringSplitOptions.None));
									break;
								case "CmDomainQ":
									Dictionary<string, List<SemDomQuestion>> questionsDict = GetExtraDataWsDict<SemDomQuestion>("Questions");
									SemDomQuestion question = GetQuestion(reader, out ws);
									List<SemDomQuestion> questions = GetListFromDict<SemDomQuestion>(questionsDict, ws);
									questions.Add(question);
									break;
							}
							break;
						}
					case XmlNodeType.EndElement:
						{
							if (reader.LocalName == "CmSemanticDomain")
							{
								Key = AbbrevByWs(KeyWs);
								reader.Read(); // Skip past the closing element before returning
								return;
							}
							break;
						}
				}
			}
		}

		protected override void PopulatePossibilityFromExtraData(ICmPossibility poss)
		{
			ICmSemanticDomain semdom = poss as ICmSemanticDomain;
			if (semdom == null)
				return;
			ILgWritingSystemFactory wsf = semdom.Cache.WritingSystemFactory;
			var questionFactory = semdom.Cache.ServiceLocator.GetInstance<ICmDomainQFactory>();
			Dictionary<string, List<SemDomQuestion>> questionsByWs = GetExtraDataWsDict<SemDomQuestion>("Questions");
			// This dict looks like {"en": (question 1, question 2...)} but each question object wants to get data that
			// looks more or less like {"en": "the question in English", "fr": "la question en francais"}...
			// So first we ensure that there are enough question objects available, and then we'll access them by index
			// as we step through the writing systems.
			int numQuestions = questionsByWs.Values.Select(questionList => questionList.Count).Max();
			while (semdom.QuestionsOS.Count < numQuestions)
			{
				semdom.QuestionsOS.Add(questionFactory.Create());
			}
			foreach (string ws in questionsByWs.Keys)
			{
				int wsId = wsf.GetWsFromStr(ws);
				int i = 0;
				foreach (SemDomQuestion qStruct in questionsByWs[ws])
				{
					// TODO: Find out what set_String() would do with a null value. Would it remove the string?
					// If it would *remove* it, then we might be able to replace string.Empty with nulls in the code below
					// Right now, just be extra-cautious and ensure we never put nulls into set_String().
					semdom.QuestionsOS[i].Question.set_String(wsId, qStruct.Question ?? string.Empty);
					semdom.QuestionsOS[i].ExampleSentences.set_String(wsId, qStruct.ExampleSentences ?? string.Empty);
					semdom.QuestionsOS[i].ExampleWords.set_String(wsId, qStruct.ExampleWords ?? string.Empty);
					i++;
				}
			}
		}
	}
}
