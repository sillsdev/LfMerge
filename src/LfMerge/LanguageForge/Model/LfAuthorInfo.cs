// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Bson;

namespace LfMerge.LanguageForge.Model
{
	public class LfAuthorInfo : LfFieldBase
	{
		public ObjectId? CreatedByUserRef { get; set; }
		public DateTime CreatedDate { get; set; }
		public ObjectId? ModifiedByUserRef { get; set; }
		public DateTime ModifiedDate { get; set; }
	}
}

