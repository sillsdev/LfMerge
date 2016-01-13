// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using LfMerge.LanguageForge.Config;

namespace LfMerge
{
	[BsonIgnoreExtraElements]
	public class MongoProjectRecord
	{
		public const string ProjectsCollectionName = "projects";

		public ObjectId Id { get; set; }
		public string InterfaceLanguageCode { get; set; }
		public string LanguageCode { get; set; }
		public string ProjectCode { get; set; }
		public string ProjectName { get; set; }
		public LfProjectConfig Config { get; set; }
	}
}

/* Mongo project records have the following fields, but we don't need to map all of them:
{ "_id" : "_id", "value" : null }
{ "_id" : "allowAudioDownload", "value" : null }
{ "_id" : "allowInviteAFriend", "value" : null }
{ "_id" : "appName", "value" : null }
{ "_id" : "config", "value" : null }
{ "_id" : "dateCreated", "value" : null }
{ "_id" : "dateModified", "value" : null }
{ "_id" : "featured", "value" : null }
{ "_id" : "inputSystems", "value" : null }
{ "_id" : "interfaceLanguageCode", "value" : null }
{ "_id" : "isArchived", "value" : null }
{ "_id" : "language", "value" : null }
{ "_id" : "languageCode", "value" : null }
{ "_id" : "liftFilePath", "value" : null }
{ "_id" : "ownerRef", "value" : null }
{ "_id" : "projectCode", "value" : null }
{ "_id" : "projectName", "value" : null }
{ "_id" : "siteName", "value" : null }
{ "_id" : "userJoinRequests", "value" : null }
{ "_id" : "userProperties", "value" : null }
*/

