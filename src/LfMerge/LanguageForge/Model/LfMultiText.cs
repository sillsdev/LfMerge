// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.COMInterfaces;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfMultiText : Dictionary<string, LfStringField> // Note: NOT derived from LfFieldBase
	{
		public static LfMultiText FromFdoMultiString(IMultiAccessorBase other, IWritingSystemManager wsManager)
		{
			LfMultiText newInstance = new LfMultiText();
			foreach (int wsid in other.AvailableWritingSystemIds)
			{
				string wsstr = wsManager.GetStrFromWs(wsid);
				ITsString value = other.get_String(wsid);
				newInstance.Add(wsstr, new LfStringField { Value = value.Text });
				// TODO: Deal with runs; using just ITsString.Text will discard any useful information found in Run elements, if present.
				// Example code follows:
				/*
				foreach (TsRunPart run in value.Runs())
				{
					if (run.Props.Style() == null)
						Console.WriteLine("No style");
					else
						Console.WriteLine("Run style {0}", run.Props.Style());
					Console.WriteLine("Run writing system {0}", wsManager.GetStrFromWs(run.Props.GetWs()));
					Console.WriteLine("Run text {0}", run.Text);
				}
				*/
			}
			return newInstance;
		}

		public static LfMultiText FromSingleStringMapping(string key, string value)
		{
			return new LfMultiText { { key, new LfStringField { Value = value } } };
		}

		public static LfMultiText FromSingleITsStringMapping(string key, ITsString value)
		{
			if (value == null || value.Text == null) return null;
			return new LfMultiText { { key, new LfStringField { Value = value.Text } } };
		}

		public Dictionary<string, string> AsStringDictionary()
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			foreach (KeyValuePair<string, LfStringField> kv in this)
			{
				result.Add(kv.Key, kv.Value.ToString());
			}
			return result;
		}
	}
}

