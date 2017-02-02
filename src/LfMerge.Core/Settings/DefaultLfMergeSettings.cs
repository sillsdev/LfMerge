// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge.Core.Settings
{
	public static class DefaultLfMergeSettings
	{
		public const string DefaultIniText = @"
BaseDir = /tmp/LfMerge.TestApp
WebworkDir = webwork
TemplatesDir = Templates
MongoHostname = localhost
MongoPort = 27017
MongoMainDatabaseName = scriptureforge
MongoDatabaseNamePrefix = sf_
VerboseProgress = false
PhpSourcePath = /var/www/languageforge.org/htdocs
";
		// optional, usually not set: LanguageDepotRepoUri =
	}
}

