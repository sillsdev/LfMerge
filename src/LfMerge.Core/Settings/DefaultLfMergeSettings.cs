// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.Settings
{
	public static class DefaultLfMergeSettings
	{
		public const string BaseDir = "/var/lib/languageforge/lexicon/sendreceive";
		public const string WebworkDir = "webwork";
		public const string TemplatesDir = "Templates";
		public const string MongoHostname = "localhost";
		public const int MongoPort = 27017;
		public const string MongoMainDatabaseName = "scriptureforge";
		public const string MongoDatabaseNamePrefix = "sf_";
		public const bool VerboseProgress = false;
		public const string LanguageDepotRepoUri = "";  // optional, usually not set
	}
}

