// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge.Core.Actions.Infrastructure;
using LfMerge.Core.Logging;
using LfMerge.Core.MongoConnector;
using LfMerge.Core.LanguageForge.Model;
using Newtonsoft.Json;
using Palaso.Progress;

namespace LfMerge.Core.DataConverters
{
	public class ConvertMongoToFdoComments
	{
		private IMongoConnection _conn;
		private ILfProject _project;
		private ILogger _logger;
		private IProgress _progress;
		public ConvertMongoToFdoComments(IMongoConnection conn, ILfProject proj, ILogger logger, IProgress progress)
		{
			_conn = conn;
			_project = proj;
			_logger = logger;
			_progress = progress;
			// TODO: Are there any other constructor parameters that we need?
		}
		public void DoSomethingAndGiveThisABetterName() // TODO: Give this a better name
		{
			var jsonSettings = new JsonSerializerSettings
			{
				DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
			};
			// JsonSerializer json = JsonSerializer.CreateDefault();
			foreach (LfComment comment in _conn.GetComments(_project))
			{
				string commentJson = JsonConvert.SerializeObject(comment, jsonSettings);
				_logger.Debug("Got some json: {0}", commentJson);
			}
			string allCommentsJson = JsonConvert.SerializeObject(_conn.GetComments(_project), jsonSettings);
			_logger.Debug("The json for ALL comments would be: {0}", allCommentsJson);
			_logger.Debug("About to call LfMergeBridge with that JSON...");
			string bridgeOutput;
			CallLfMergeBridge(allCommentsJson, out bridgeOutput);
			string guidMappingsStr = GetPrefixedStringFromLfMergeBridgeOutput(bridgeOutput, "New reply ID->Guid mappings: ");
			Dictionary<string, string> uniqIdToGuidMappings = ParseGuidMappings(guidMappingsStr);
			foreach (KeyValuePair<string,string> kv in uniqIdToGuidMappings)
			{
				_logger.Debug("Would map uniqid {0} to GUID {1}", kv.Key, kv.Value);
			}
			// _conn.SetCommentReplyGuids(_project, uniqIdToGuidMappings);  // Uncomment when we're ready to test (Tuesday morning)
		}

		public bool CallLfMergeBridge(string bridgeInput, out string bridgeOutput)
		{
			bridgeOutput = string.Empty;
			using (var tmpFile = new Palaso.IO.TempFile(bridgeInput))
			{
				var options = new Dictionary<string, string>
				{
					{"-p", _project.FwDataPath},
					{"-i", tmpFile.Path}
				};
				if (!LfMergeBridge.LfMergeBridge.Execute("Language_Forge_Write_To_Chorus_Notes", _progress,
					options, out bridgeOutput))
				{
					_logger.Error("Got an error from Language_Forge_Write_To_Chorus_Notes: {0}", bridgeOutput);
					return false;
				}
				else
				{
					_logger.Debug("Good  output from Language_Forge_Write_To_Chorus_Notes: {0}", bridgeOutput);
					return true;
				}
			}
		}

		public string GetPrefixedStringFromLfMergeBridgeOutput(string lfMergeBridgeOutput, string prefix)
		{
			if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(lfMergeBridgeOutput))
			{
				return string.Empty;
			}
			string result = LfMergeBridgeServices.GetLineContaining(lfMergeBridgeOutput, prefix);
			// NOTE: This could fail if the prefix is present twice and we actually wanted the second line, so this isn't the best solution.
			// The right solution is to move this function to LfBridgeServices so we can use GetLinesFromLfBridge, and keep looping.
			// But this is good enough for testing.
			// TODO: Do it right.
			if (result.StartsWith(prefix))
			{
				return result.Substring(prefix.Length);
			}
			else
			{
				return string.Empty; // If the "prefix" wasn't actually a prefix, this wasn't the string we wanted.
			}
		}

		public Dictionary<string, string> ParseGuidMappings(string input)
		{
			string[] parts = input.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			var result = new Dictionary<string, string>();
			foreach (string part in parts)
			{
				string[] kv = part.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
				if (kv.Length == 2)
				{
					result[kv[0]] = kv[1];
				}
			}
			return result;
		}
	}
}
