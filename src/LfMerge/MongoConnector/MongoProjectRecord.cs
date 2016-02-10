// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using LfMerge.LanguageForge.Config;
using LfMerge.LanguageForge.Model;
using System.Collections.Generic;

namespace LfMerge.MongoConnector
{
	[BsonIgnoreExtraElements]
	public class MongoProjectRecord
	{
		public ObjectId Id { get; set; }
		public Dictionary<string, LfInputSystemRecord> InputSystems { get; set; }
		public string InterfaceLanguageCode { get; set; }
		public string LanguageCode { get; set; }
		public string ProjectCode { get; set; }
		public string ProjectName { get; set; }
		public LfProjectConfig Config { get; set; }

		public MongoProjectRecord()
		{
			InputSystems = new Dictionary<string, LfInputSystemRecord>();
		}
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

