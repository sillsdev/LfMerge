// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using System.Linq;
using System;

namespace LfMerge.Core.LanguageForge.Model
{
	public class LfStringArrayField : LfFieldBase
	{
		private IList<LfStringField> _values = new List<LfStringField>();

		public IEnumerable<Guid> LcmGuids { get { return _values.Select(v => v.LcmGuid); } }
		public List<string> Values { get; set; }
		public bool IsEmpty { get { return Values.Count <= 0; } }

		private LfStringArrayField() { }

		public static LfStringArrayField CreateFrom(IEnumerable<LfStringField> source)
		{
			if (source == null)
				throw new ApplicationException("Tried to create LfStringArrayField with no source.");
			return new LfStringArrayField { _values = source.Where(f => f != null).ToList() };
		}
	}
}

