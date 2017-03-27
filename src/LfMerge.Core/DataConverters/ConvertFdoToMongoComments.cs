// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using Newtonsoft.Json;
using Palaso.Progress;
using SIL.FieldWorks.FDO;

namespace LfMerge.Core.DataConverters
{
	public class ConvertFdoToMongoComments
	{
		private IMongoConnection _conn;
		private ILfProject _project;
		private ILogger _logger;
		private IProgress _progress;
		private FwServiceLocatorCache _servLoc;
		private MongoProjectRecordFactory _factory;
		public ConvertFdoToMongoComments(IMongoConnection conn, ILfProject proj, ILogger logger, IProgress progress, MongoProjectRecordFactory factory)
		{
			_conn = conn;
			_project = proj;
			_logger = logger;
			_progress = progress;
			_factory = factory;
		}

		public void DoSomethingAndGiveThisABetterName() // TODO: Give this a better name
		{
			LfProjectConfig config = _factory.Create(_project).Config;
			FieldLists fieldConfigs = FieldListsForEntryAndSensesAndExamples(config);

			var jsonSettings = new JsonSerializerSettings
			{
				DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
			};
			string bridgeOutput;
			if (CallLfMergeBridge("", out bridgeOutput))
			{
				List<LfComment> comments = JsonConvert.DeserializeObject<List<LfComment>>(bridgeOutput, jsonSettings);
				foreach (LfComment comment in comments)
				{
					// Regarding.Word is set from LfMergeBridge to the FLEx "label", but that's in a different format from what LF wants
					if (comment.Regarding != null)
					{
						Guid guid;
						if (Guid.TryParse(comment.Regarding.TargetGuid ?? "", out guid))
						{
							// The GUID in Chorus notes MIGHT be an entry, or it might be a sense or an example sentence.
							// We want to handle these three cases differently -- see FromTargetGuid below.
							comment.Regarding = FromTargetGuid(guid, fieldConfigs);
						}
					}
					_logger.Debug("Comment regarding field {0} (containing {1}) of word {2} (meaning {3}) has content {4}",
						comment.Regarding.FieldNameForDisplay,
						comment.Regarding.FieldValue,
						comment.Regarding.Word,
						comment.Regarding.Meaning,
						comment.Content);
				}
			}
			else
			{
				// Failure, which has already been logged so we don't need to log it again
			}
		}

		public struct FieldLists
		{
			public LfConfigFieldList entryConfig;
			public LfConfigFieldList senseConfig;
			public LfConfigFieldList exampleConfig;
		}

		public FieldLists FieldListsForEntryAndSensesAndExamples(LfProjectConfig config)
		{
			FieldLists result = new FieldLists();
			result.entryConfig = config.Entry;
			result.senseConfig = null;
			result.exampleConfig = null;
			if (result.entryConfig != null && result.entryConfig.Fields.ContainsKey("senses"))
				result.senseConfig = result.entryConfig.Fields["senses"] as LfConfigFieldList;
			if (result.senseConfig != null && result.senseConfig.Fields.ContainsKey("examples"))
				result.exampleConfig = result.senseConfig.Fields["examples"] as LfConfigFieldList;
			return result;
		}

		public string Definition(ILexSense sense)
		{
			if (sense == null
			 || sense.Definition == null
			 || sense.Definition.BestAnalysisVernacularAlternative == null)
			{
				return "";
			}
			return sense.Definition.BestAnalysisVernacularAlternative.Text ?? "";
		}

		public string ExampleValue(ILexExampleSentence example)
		{
			if (example == null
			 || example.Example == null
			 || example.Example.BestAnalysisVernacularAlternative == null)
			{
				return "";
			}
			return example.Example.BestAnalysisVernacularAlternative.Text ?? "";
		}

		public string TitleCase(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";
			return Char.ToUpper(input[0]).ToString() + input.Substring(1);
		}

		public string FieldNameForDisplay(string field, LfConfigFieldList fieldList)
		{
			LfConfigFieldBase fieldConfig;
			if (fieldList != null && fieldList.Fields.TryGetValue(field, out fieldConfig))
			{
				return fieldConfig.Label;
			}
			else
			{
				return TitleCase(field); // Fallback
			}
		}

		public LfCommentRegarding FromTargetGuid(Guid guidOfUnknownFdoObject, FieldLists fieldConfigs)
		{
			var result = new LfCommentRegarding(); // Worst case, we'll return this empty object rather than null
			ICmObject fdoObject = _servLoc.ObjectRepository.GetObject(guidOfUnknownFdoObject);
			if (fdoObject == null) return result;
			result.TargetGuid = fdoObject.Guid.ToString();
			switch (fdoObject.ClassID)
			{
				case LexEntryTags.kClassId:
				{
					var entry = fdoObject as ILexEntry;
					if (entry != null) // Paranoia; should never happen, but check anyway
					{
						result.Word = entry.ShortName;
						result.Meaning = entry.NumberOfSensesForEntry < 1 ? "" : Definition(entry.SensesOS[0]);
					}
					break;
				}
				case LexSenseTags.kClassId:
				{
					var sense = fdoObject as ILexSense;
					if (sense == null || sense.Entry == null) // Paranoia; should never happen, but check anyway
						return result;
					ILexEntry entry = sense.Entry;
					result.TargetGuid = entry.Guid.ToString();
					result.Word = entry.ShortName;
					result.Meaning = Definition(sense);
					result.Field = MagicStrings.LfFieldNameForDefinition; // Even if it's really the gloss, we'll say it's the definition for LF display purposes
					result.FieldNameForDisplay = FieldNameForDisplay(result.Field, fieldConfigs.senseConfig);
					result.FieldValue = result.Meaning;
					break;
				}
				case LexExampleSentenceTags.kClassId:
				{
					var example = fdoObject as ILexExampleSentence;
					if (example == null || example.Owner == null) // Paranoia; should never happen, but check anyway
						return result;
					var sense = example.Owner as ILexSense; // Example sentences are always owned by senses
					if (sense == null || sense.Entry == null) // Paranoia; should never happen, but check anyway
						return result;
					ILexEntry entry = sense.Entry;
					result.TargetGuid = entry.Guid.ToString();
					result.Word = entry.ShortName;
					result.Meaning = Definition(sense);
					result.Field = MagicStrings.LfFieldNameForExampleSentence; // Even if it's really the gloss, we'll say it's the definition for LF display purposes
					result.FieldNameForDisplay = FieldNameForDisplay(result.Field, fieldConfigs.exampleConfig);
					result.FieldValue = ExampleValue(example);
					break;
				}
				default:
				{
					// Last-ditch effort: climb the owner chain and maybe we'll hit a LexEntry
					while (fdoObject.Owner != null && fdoObject.ClassID != LexEntryTags.kClassId)
						fdoObject = fdoObject.Owner;
					if (fdoObject != null && fdoObject.ClassID == LexEntryTags.kClassId)
					{
						var entry = fdoObject as ILexEntry;
						if (entry != null)
						{
							result.Word = entry.ShortName;
							result.Meaning = entry.NumberOfSensesForEntry < 1 ? "" : Definition(entry.SensesOS[0]);
						}
					}
					// But if we didn't, then just give up
					break;
				}
			}
			return result;
		}

		public ILexEntry OwningEntry(Guid guidOfUnknownFdoObject)
		{
			Guid result = Guid.Empty;
			var fdoObject = _servLoc.ObjectRepository.GetObject(guidOfUnknownFdoObject);
			if (fdoObject == null)
				return null;
			switch (fdoObject.ClassID)
			{
				case LexEntryTags.kClassId:
				{
					return fdoObject as ILexEntry;
				}
				case LexSenseTags.kClassId:
				{
					var sense = fdoObject as ILexSense;
					if (sense == null || sense.Entry == null)
						return null;
					return sense.Entry;
				}
				case LexExampleSentenceTags.kClassId:
				{
					var example = fdoObject as ILexExampleSentence;
					if (example == null || example.Owner == null)
						return null;
					var sense = example.Owner as ILexSense;
					if (sense == null || sense.Entry == null)
						return null;
					return sense.Entry;
				}
				case CmPictureTags.kClassId:
				{
					var picture = fdoObject as ICmPicture;
					return picture.Owner as ILexEntry;
				}
				default:
				{
					// Last-ditch effort: climb the owner chain and maybe we'll hit a LexEntry
					while (fdoObject.Owner != null && fdoObject.ClassID != LexEntryTags.kClassId)
						fdoObject = fdoObject.Owner;
					if (fdoObject != null && fdoObject.ClassID == LexEntryTags.kClassId)
					{
						return fdoObject as ILexEntry;
					}
					// But if we didn't, then just give up
					return null;
				}
			}
		}

		public bool CallLfMergeBridge(string bridgeInput, out string bridgeOutput)
		{
			// Call into LF Bridge to do the work.
			bridgeOutput = string.Empty;
			var options = new Dictionary<string, string>
			{
				{"-p", _project.FwDataPath},
			};
			if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Get_Chorus_Notes", _progress,
				options, out bridgeOutput))
			{
				_logger.Error("Got an error from Language_Forge_Get_Chorus_Notes: {0}", bridgeOutput);
				return false;
			}
			else
			{
				_logger.Debug("Got the JSON from Language_Forge_Get_Chorus_Notes: {0}", bridgeOutput);
				return true;
			}
		}
	}
}
