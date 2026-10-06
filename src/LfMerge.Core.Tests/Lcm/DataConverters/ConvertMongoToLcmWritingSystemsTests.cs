using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Config;
using LfMerge.Core.LanguageForge.Model;
using LfMerge.Core.MongoConnector;
using LfMergeBridge.LfMergeModel;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// Language Forge stores a writing system tag exactly as it was typed, but LCM canonicalizes a
	/// tag when it creates a writing system and thereafter knows that writing system by its Id. LCM's
	/// own lookups -- WritingSystemManager.TryGet, GetWsFromStr -- match an Id regardless of case
	/// but otherwise literally, so a tag differing from the Id in structure, a subtag absorbed or
	/// dropped, is not found by them; LanguageTags.WsIdFromLfTag finds it.
	///
	/// testlangproj already contains "qaa-x-kal" and "qaa-Zxxx-x-kal-audio", so a non-canonical LF
	/// spelling of either exercises the mismatch without adding to the shared fixture project. The
	/// tests that need a writing system of their own add it under a tag no other test uses, since
	/// what is added to the fixture's LCM stays for the rest of the run.
	/// </summary>
	public class ConvertMongoToLcmWritingSystemsTests : LcmTestBase
	{
		// The apltw2016 shape: the private-use section repeats the "qaa" language subtag, which
		// canonicalizing absorbs (apltw2016 itself has "qaa-x-qaa-v", which becomes "qaa-x-v").
		private const string RedundantPrivateUseTag = "qaa-x-qaa-kal";
		private const string RedundantPrivateUseId = "qaa-x-kal";

		// The shape found in the testlangproj-derived projects, where canonicalizing only changes case.
		// This one ALREADY works, because both LCM lookups (TryGet and GetWsFromStr) are
		// case-insensitive; it is here as a guard so that a fix for the structural case does not
		// regress it. Only differences in structure -- a subtag absorbed or dropped -- go unfound.
		private const string MixedCaseTag = "qaa-Zxxx-x-kal-AUDIO";
		private const string MixedCaseId = "qaa-Zxxx-x-kal-audio";

		/// <summary>
		/// Adds one input system to the mock project record, and returns an action that puts the
		/// record back as it was. Each test gets a fresh record, so the restore is only tidiness;
		/// what does outlive a test is a writing system it adds to LCM, which is why the tests that
		/// add one each use tags of their own.
		/// </summary>
		private Action AddLfInputSystem(string tag)
		{
			MongoProjectRecord record = _recordFactory.Create(_lfProj);
			Assert.That(record, Is.Not.Null, "the mock project record factory should supply a record");
			Assert.That(record.InputSystems.ContainsKey(tag), Is.False,
				"the fixture's input systems should not already contain {0}", tag);

			record.InputSystems[tag] = new LfInputSystemRecord {
				Abbreviation = tag,
				Tag = tag,
				LanguageName = "Unlisted Language",
				IsRightToLeft = false
			};
			return () => record.InputSystems.Remove(tag);
		}

		/// <summary>
		/// Adds a tag to one config field's input systems, found by its path through the entry's
		/// field lists, and returns an action that takes it out again. Like the input systems, the
		/// config comes fresh with each test's record, so this too is only tidiness.
		/// </summary>
		private Action AddToConfigField(string tag, params string[] path)
		{
			LfConfigFieldBase field = _recordFactory.Create(_lfProj).Config.Entry;
			foreach (string key in path)
				field = ((LfConfigFieldList)field).Fields[key];
			var multiText = (LfConfigMultiText)field;
			multiText.InputSystems.Add(tag);
			return () => multiText.InputSystems.Remove(tag);
		}

		private IEnumerable<string> LcmWritingSystemIds()
		{
			return _cache.ServiceLocator.WritingSystemManager.WritingSystems.Select(ws => ws.Id);
		}

		[TestCase(RedundantPrivateUseTag, RedundantPrivateUseId)]
		[TestCase(MixedCaseTag, MixedCaseId)]
		public void LfWsToLcmWs_NonCanonicalTag_ReusesTheCanonicalWritingSystem(string lfTag, string canonicalId)
		{
			// Setup: LF holds the writing system under a non-canonical tag, while the LCM project
			// already holds it under the canonical id that LCM derives from that same tag.
			Assert.That(LcmWritingSystemIds(), Contains.Item(canonicalId),
				"testlangproj should already contain {0}", canonicalId);
			int countBefore = LcmWritingSystemIds().Count(id => id == canonicalId);
			Action restore = AddLfInputSystem(lfTag);

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify: the existing writing system was reused, not duplicated, and the raw LF tag
				// did not become an id of its own.
				Assert.That(LcmWritingSystemIds().Count(id => id == canonicalId), Is.EqualTo(countBefore),
					"{0} should still appear exactly once", canonicalId);
				Assert.That(LcmWritingSystemIds(), Does.Not.Contain(lfTag),
					"the non-canonical tag {0} should not be stored as an id of its own", lfTag);
			}
			finally
			{
				restore();
			}
		}

		[Test]
		public void SetMultiStringFrom_NonCanonicalTagInLexicon_WritesToTheCanonicalWritingSystem()
		{
			// Setup: a definition keyed by the non-canonical tag. LfMultiText used to resolve each key
			// with GetWsFromStr and skip anything that returned 0, so an unresolved key dropped the
			// value -- and the clearing pass then blanked whatever LCM had for that field.
			const string newDefinition = "Definition stored under a non-canonical writing system tag";
			var data = new SampleData();
			data.bsonTestData["senses"][0]["definition"] = new BsonDocument {
				{ RedundantPrivateUseTag, new BsonDocument { { "value", newDefinition } } }
			};
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);

			int wsId = _cache.WritingSystemFactory.GetWsFromStr(RedundantPrivateUseId);
			Assert.That(wsId, Is.Not.EqualTo(0), "{0} should resolve in testlangproj", RedundantPrivateUseId);

			// Exercise
			SutMongoToLcm.Run(_lfProj);

			// Verify
			Guid expectedGuid = Guid.Parse(data.bsonTestData["guid"].AsString);
			var entry = _cache.ServiceLocator.GetObject(expectedGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.SensesOS[0].Definition.get_String(wsId).Text, Is.EqualTo(newDefinition),
				"the definition should have been written to the canonical writing system {0}", RedundantPrivateUseId);
		}

		/// <summary>
		/// Text in a built-in multitext field under a key that names no writing system LCM has has
		/// nowhere to go. It is skipped, as it always was, but no longer silently: the custom fields
		/// and the single-string fields already said so, and this, the commonest path, did not.
		/// </summary>
		[Test]
		public void SetMultiStringFrom_KeyNamingNoWritingSystem_IsSkippedWithAWarning()
		{
			const string tag = "zzz-x-nonesuch";
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, tag), Is.EqualTo(0));
			var data = new SampleData();
			data.bsonTestData["senses"][0]["definition"][tag] = new BsonDocument { { "value", "a definition with nowhere to go" } };
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);

			SutMongoToLcm.Run(_lfProj);

			Assert.That(_env.Logger.GetMessages(),
				Does.Contain("MongoToLcm: skipping text under \"zzz-x-nonesuch\" (a definition with nowhere to go)"));
		}

		[Test]
		public void SingleStringField_TextInNoKnownWritingSystem_KeepsTheLcmValueAndSyncsTheRest()
		{
			// Setup: LCM holds a scientific name, and then LF holds one only in a writing system LCM
			// has never heard of. Building that into a string in writing system 0 threw, abandoning
			// the rest of the entry half-written; returning null instead would clear the field and
			// delete what LCM holds. Neither is acceptable.
			const string original = "Homo sapiens";
			const string laterNote = "A note set after the scientific name";
			var data = new SampleData();
			BsonDocument sense = data.bsonTestData["senses"][0].AsBsonDocument;
			sense["scientificName"] = new BsonDocument { { "en", new BsonDocument { { "value", original } } } };
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			SutMongoToLcm.Run(_lfProj);

			sense["scientificName"] = new BsonDocument {
				{ "zzz-x-nonesuch", new BsonDocument { { "value", "Unplaceable" } } } };
			// Converted after the scientific name, so it only arrives if the entry is not abandoned.
			sense["sociolinguisticsNote"] = new BsonDocument {
				{ "en", new BsonDocument { { "value", laterNote } } } };
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow.AddMinutes(1);
			_conn.UpdateMockLfLexEntry(data.bsonTestData);

			// Exercise
			SutMongoToLcm.Run(_lfProj);

			// Verify
			var entry = _cache.ServiceLocator.GetObject(Guid.Parse(data.bsonTestData["guid"].AsString)) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense lcmSense = entry.SensesOS[0];
			Assert.That(lcmSense.ScientificName.Text, Is.EqualTo(original),
				"text that cannot be placed must not replace or clear what LCM holds");
			Assert.That(lcmSense.SocioLinguisticsNote.get_String(_wsEn).Text, Is.EqualTo(laterNote),
				"the rest of the entry should still have been converted");
			Assert.That(_env.Logger.GetMessages(), Does.Contain(
				"MongoToLcm: skipping text under \"zzz-x-nonesuch\" (Unplaceable), which names no writing system LCM has; " +
				"leaving the field as it was"));
		}

		/// <summary>
		/// A single-string field whose text is all under a key passed over for an empty one naming
		/// the same writing system -- the config's spelling, which LF's editor fills in empty --
		/// keeps what LCM holds, and the log says so for that reason, not that LCM lacks the
		/// writing system.
		/// </summary>
		[Test]
		public void SingleStringField_TextOnlyUnderAKeyPassedOverForAnEmptyOne_KeepsTheLcmValueAndSaysWhy()
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId("ro-Latn-MD", "ro-MD");
			const string original = "Homo sapiens";
			var data = new SampleData();
			BsonDocument sense = data.bsonTestData["senses"][0].AsBsonDocument;
			sense["scientificName"] = new BsonDocument { { "en", new BsonDocument { { "value", original } } } };
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			SutMongoToLcm.Run(_lfProj);

			Action restore = AddToConfigField("ro-MD", "senses", "scientificName");
			try
			{
				sense["scientificName"] = new BsonDocument {
					{ "ro-Latn-MD", new BsonDocument { { "value", "the export's value" } } },
					{ "ro-MD", new BsonDocument { { "value", "" } } } };
				data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow.AddMinutes(1);
				_conn.UpdateMockLfLexEntry(data.bsonTestData);

				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				var entry = _cache.ServiceLocator.GetObject(Guid.Parse(data.bsonTestData["guid"].AsString)) as ILexEntry;
				Assert.That(entry.SensesOS[0].ScientificName.Text, Is.EqualTo(original));
				Assert.That(_env.Logger.GetMessages(), Does.Contain(
					"MongoToLcm: keys \"ro-Latn-MD\" and \"ro-MD\" name the same writing system, and \"ro-MD\", the one to " +
					"write, is empty; leaving the field as it was rather than write \"ro-Latn-MD\" (the export's value)"));
				Assert.That(_env.Logger.GetMessages(), Does.Not.Contain("names no writing system LCM has"));
			}
			finally
			{
				restore();
			}
		}

		[TestCase(RedundantPrivateUseTag, RedundantPrivateUseId)]
		[TestCase(MixedCaseTag, MixedCaseId)]
		[TestCase("fr-Latn", "fr")]
		public void WsIdFromLfTag_NonCanonicalTag_ResolvesToTheCanonicalWritingSystem(string lfTag, string canonicalId)
		{
			int expected = _cache.WritingSystemFactory.GetWsFromStr(canonicalId);
			Assert.That(expected, Is.Not.EqualTo(0), "{0} should resolve in testlangproj", canonicalId);
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, lfTag), Is.EqualTo(expected));
		}

		/// <summary>
		/// Adds a writing system whose Id is spelled differently from its LanguageTag, the way LCM
		/// loads an older .ldml file: brb-flex-2022's "km-Khmr-KH.ldml" has km plus KH for its
		/// identity, so it is held as Id "km-Khmr-KH" with LanguageTag "km-KH". The repository
		/// keeps an Id as given whenever it is equivalent to the LanguageTag.
		/// </summary>
		private CoreWritingSystemDefinition AddWritingSystemWithId(string id, string languageTag)
		{
			WritingSystemManager wsManager = _cache.ServiceLocator.WritingSystemManager;
			CoreWritingSystemDefinition ws = wsManager.Create(languageTag);
			ws.Id = id;
			wsManager.Set(ws);
			Assert.That(ws.Id, Is.EqualTo(id), "precondition: LCM should hold the writing system as {0}", id);
			Assert.That(ws.LanguageTag, Is.EqualTo(languageTag));
			return ws;
		}

		[Test]
		public void WsIdFromLfTag_ExactIdWinsOverAnotherWritingSystemWithTheCanonicalId()
		{
			// The brb-flex-2022 layout: the real writing system under a non-canonical Id, and an
			// unused second one whose Id is the canonical form. Text keyed by the real one's Id is
			// the real one's text; resolving the canonical form first sent it to the other, and the
			// clearing pass then deleted the real writing system's text as missing from LF.
			CoreWritingSystemDefinition real = AddWritingSystemWithId("de-Latn-AT", "de-AT");
			CoreWritingSystemDefinition stray = _cache.ServiceLocator.WritingSystemManager.Set("de-AT");
			Assert.That(stray.Handle, Is.Not.EqualTo(real.Handle));

			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, "de-Latn-AT"), Is.EqualTo(real.Handle));
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, "de-AT"), Is.EqualTo(stray.Handle));
		}

		[Test]
		public void WsIdFromLfTag_CanonicalTagFindsAWritingSystemWhoseIdIsSpelledOtherwise()
		{
			// The spt-flex layout: LCM holds "hi-Deva-IN" and LF's input systems call it "hi-IN",
			// which is neither the Id nor its canonical form spelled as an Id.
			CoreWritingSystemDefinition real = AddWritingSystemWithId("fr-Latn-CA", "fr-CA");
			Assert.That(_cache.WritingSystemFactory.GetWsFromStr("fr-CA"), Is.EqualTo(0),
				"precondition: no writing system has the Id fr-CA");

			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, "fr-CA"), Is.EqualTo(real.Handle));
		}

		[Test]
		public void LfWsToLcmWs_InputSystemSpelledAsTheLanguageTag_DoesNotDuplicateTheWritingSystem()
		{
			// The spt-flex layout: LCM holds "hi-Deva-IN", whose LanguageTag is "hi-IN", and LF's
			// input systems -- which LfMerge used to export by LanguageTag -- call it "hi-IN". GetOrSet looks
			// only at Ids, so it created a second writing system "hi-IN" and put it in the lists.
			CoreWritingSystemDefinition real = AddWritingSystemWithId("pt-Latn-AO", "pt-AO");
			Action restore = AddLfInputSystem("pt-AO");

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				Assert.That(LcmWritingSystemIds(), Does.Not.Contain("pt-AO"),
					"no duplicate writing system should be created for the input system");
				Assert.That(_cache.ServiceLocator.WritingSystemManager.WritingSystems
					.Count(ws => ws.LanguageTag == "pt-AO"), Is.EqualTo(1));
				Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, "pt-AO"), Is.EqualTo(real.Handle),
					"LF text keyed \"pt-AO\" should go to the existing writing system");
			}
			finally
			{
				restore();
			}
		}

		/// <summary>
		/// An input system spelled by Id -- as every project LfMerge set up under FieldWorks 8 has
		/// them -- for a writing system whose Id differs from its LanguageTag. Replace looked the
		/// writing system up by LanguageTag, which either found nothing and re-registered it under a
		/// fresh handle, or found the unused writing system with that LanguageTag as its Id and
		/// evicted it, the real one taking over its handle.
		/// </summary>
		[TestCase("es-Latn-MX", "es-MX", false)]
		[TestCase("it-Latn-CH", "it-CH", true)]
		public void LfWsToLcmWs_ExistingWritingSystemWhoseIdIsNotItsLanguageTag_KeepsItsHandle(
			string id, string languageTag, bool withUnusedNamesake)
		{
			WritingSystemManager wsManager = _cache.ServiceLocator.WritingSystemManager;
			CoreWritingSystemDefinition real = AddWritingSystemWithId(id, languageTag);
			int realHandle = real.Handle;
			CoreWritingSystemDefinition namesake = withUnusedNamesake ? wsManager.Set(languageTag) : null;
			int namesakeHandle = withUnusedNamesake ? namesake.Handle : 0;
			Action restore = AddLfInputSystem(id);

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				Assert.That(real.Handle, Is.EqualTo(realHandle), "the writing system should keep its handle");
				Assert.That(wsManager.GetWsFromStr(id), Is.EqualTo(realHandle));
				Assert.That(real.Abbreviation, Is.EqualTo(id),
					"the input system's properties should still have been applied");
				if (withUnusedNamesake)
				{
					Assert.That(wsManager.GetWsFromStr(languageTag), Is.EqualTo(namesakeHandle),
						"the other writing system with that LanguageTag should be left alone");
				}
			}
			finally
			{
				restore();
			}
		}

		/// <summary>
		/// Two LF keys naming one writing system: the export's, spelled as its Id, and one spelled
		/// as its LanguageTag. Only one can be written, and it is the one under the spelling LF's
		/// config uses, wherever it comes, since that is the one LF's editor shows; with neither
		/// spelling configured, the later one. The other is reported.
		///
		/// The export's key comes first in an entry, since an LF user's new key is added after it.
		/// Before the config is re-spelled by Id, the user's edit is under the LanguageTag and the
		/// export's value is hidden and stale; afterwards, an Id-spelled edit is made in place, and a
		/// LanguageTag key left from before is what is stale.
		/// </summary>
		// A pair each, since writing systems a test adds stay in the fixture's project
		[TestCase("languageTag", true, "sv-Latn-FI", "sv-FI", TestName = "WriteToLcm_TwoKeysForOneWritingSystem_ConfigSpelledByLanguageTag_WritesTheEdit")]
		[TestCase("id", true, "da-Latn-DE", "da-DE", TestName = "WriteToLcm_TwoKeysForOneWritingSystem_ConfigSpelledById_WritesTheEditMadeInPlace")]
		[TestCase("languageTag", false, "nb-Latn-NO", "nb-NO", TestName = "WriteToLcm_TwoKeysForOneWritingSystem_ConfigSpelling_WinsWhereverItComes")]
		[TestCase("neither", true, "fi-Latn-SE", "fi-SE", TestName = "WriteToLcm_TwoKeysForOneWritingSystem_NeitherConfigured_WritesTheLaterKey")]
		public void WriteToLcm_TwoKeysForOneWritingSystem(string configured, bool idFirst, string id, string languageTag)
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId(id, languageTag);
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, languageTag), Is.EqualTo(ws.Handle),
				"precondition: the LanguageTag spelling should resolve to the same writing system");
			var multiText = new LfMultiText();
			if (idFirst)
				multiText.Add(id, LfStringField.FromString("under the Id"));
			multiText.Add(languageTag, LfStringField.FromString("under the LanguageTag"));
			if (!idFirst)
				multiText.Add(id, LfStringField.FromString("under the Id"));
			var configuredTags = new HashSet<string>(StringComparer.Ordinal) { "en", "fr" };
			if (configured == "id")
				configuredTags.Add(id);
			else if (configured == "languageTag")
				configuredTags.Add(languageTag);
			string expected = configured == "id" ? id : configured == "languageTag" ? languageTag : (idFirst ? languageTag : id);
			var gloss = ((ILexEntry)_cache.ServiceLocator.GetObject(Guid.Parse(TestEntryGuidStr))).SensesOS[0].Gloss;
			var reported = new List<(string NotWritten, string Written)>();

			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", _cache.ActionHandlerAccessor, () =>
				multiText.WriteToLcmMultiString(gloss, _cache.WritingSystemFactory, configuredTags,
					onKeyNotWritten: (notWritten, written) => reported.Add((notWritten, written))));

			Assert.That(gloss.get_String(ws.Handle).Text, Is.EqualTo(expected == id ? "under the Id" : "under the LanguageTag"));
			Assert.That(reported, Is.EqualTo(new[] { (expected == id ? languageTag : id, expected) }));
		}

		/// <summary>
		/// The config's spellings reach every writer that chooses between two keys naming one
		/// writing system, through a whole sync: the built-in multitext fields, and picture captions
		/// with them; the single-string fields; and the multitext and string custom fields. The
		/// config's spelling comes first here, so a writer going on key order alone would write the
		/// export's value instead.
		/// </summary>
		// A pair each, since writing systems a test adds stay in the fixture's project
		[TestCase("gloss", "hu-Latn-SK", "hu-SK")]
		[TestCase("scientificName", "sk-Latn-HU", "sk-HU")]
		[TestCase("customMultiText", "sl-Latn-IT", "sl-IT")]
		[TestCase("customString", "et-Latn-FI", "et-FI")]
		public void MongoToLcm_TwoKeysForOneWritingSystem_WritesTheConfigsSpellingInEveryKindOfField(
			string field, string id, string languageTag)
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId(id, languageTag);
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, languageTag), Is.EqualTo(ws.Handle),
				"precondition: the LanguageTag spelling should resolve to the same writing system");
			var twoKeys = new BsonDocument {
				{ languageTag, new BsonDocument { { "value", "the user's edit" } } },
				{ id, new BsonDocument { { "value", "the export's value" } } } };
			var data = new SampleData();
			BsonDocument sense = data.bsonTestData["senses"][0].AsBsonDocument;
			BsonDocument customFields = data.bsonTestData["customFields"].AsBsonDocument;
			string[] configPath;
			switch (field)
			{
			case "gloss":
				sense["gloss"] = twoKeys;
				configPath = new[] { "senses", "gloss" };
				break;
			case "scientificName":
				sense["scientificName"] = twoKeys;
				configPath = new[] { "senses", "scientificName" };
				break;
			case "customMultiText":
				customFields["customField_entry_Cust_Single_Line_All"] = twoKeys;
				configPath = new[] { "customField_entry_Cust_Single_Line_All" };
				break;
			default:
				customFields["customField_entry_Cust_Single_Line"] = twoKeys;
				configPath = new[] { "customField_entry_Cust_Single_Line" };
				break;
			}
			data.bsonTestData["authorInfo"]["modifiedDate"] = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(data.bsonTestData);
			Action restore = AddToConfigField(languageTag, configPath);

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				var entry = (ILexEntry)_cache.ServiceLocator.GetObject(Guid.Parse(data.bsonTestData["guid"].AsString));
				ISilDataAccess sda = _cache.DomainDataByFlid;
				string written;
				switch (field)
				{
				case "gloss":
					written = entry.SensesOS[0].Gloss.get_String(ws.Handle).Text;
					break;
				case "scientificName":
					written = entry.SensesOS[0].ScientificName.Text;
					break;
				case "customMultiText":
					written = sda.get_MultiStringAlt(entry.Hvo, EntryCustomFieldId("Cust Single Line All"), ws.Handle).Text;
					break;
				default:
					written = sda.get_StringProp(entry.Hvo, EntryCustomFieldId("Cust Single Line")).Text;
					break;
				}
				Assert.That(written, Is.EqualTo("the user's edit"));
			}
			finally
			{
				restore();
			}
		}

		private int EntryCustomFieldId(string name)
		{
			int flid = _cache.MetaDataCacheAccessor.GetFieldId2(LexEntryTags.kClassId, name, false);
			Assert.That(flid, Is.Not.EqualTo(0), "testlangproj should have the custom field {0}", name);
			return flid;
		}

		/// <summary>
		/// The xkk-flex-2022 layout, carried all the way round: one writing system whose Id is not
		/// its LanguageTag, and a second whose Id IS that LanguageTag, each with text of its own.
		/// LfMerge used to look the first up by LanguageTag when re-registering it, evict the second
		/// and take its handle, so the two writing systems' text could end up under one of them.
		/// Each must keep its own text, in LCM and in LF, through an export, an LF edit and an
		/// import. The pair is one whose LanguageTag is the same under both SLDR datasets; xkk's own
		/// is not, and the tests run on the older one.
		/// </summary>
		[Test]
		public void RoundTrip_WritingSystemAndItsLanguageTagNamesake_EachKeepsItsOwnText()
		{
			// Setup: both writing systems in the analysis list, each with a gloss
			const string id = "ca-Latn-AD", namesakeId = "ca-AD";
			WritingSystemManager wsManager = _cache.ServiceLocator.WritingSystemManager;
			CoreWritingSystemDefinition real = AddWritingSystemWithId(id, namesakeId);
			CoreWritingSystemDefinition namesake = wsManager.Set(namesakeId);
			int realHandle = real.Handle, namesakeHandle = namesake.Handle;
			Assert.That(namesakeHandle, Is.Not.EqualTo(realHandle));
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = (ILexEntry)_cache.ServiceLocator.GetObject(entryGuid);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", _cache.ActionHandlerAccessor, () =>
			{
				_cache.LanguageProject.AddToCurrentAnalysisWritingSystems(real);
				_cache.LanguageProject.AddToCurrentAnalysisWritingSystems(namesake);
				lcmEntry.SensesOS[0].Gloss.set_String(realHandle, "glossa andorrana");
				lcmEntry.SensesOS[0].Gloss.set_String(namesakeHandle, "glossa de l'homonima");
			});

			// Exercise: export, edit both in LF, import, export again
			SutLcmToMongo.Run(_lfProj);
			LfLexEntry lfEntry = _conn.GetLfLexEntryByGuid(_lfProj, entryGuid);
			Assert.That(lfEntry.Senses[0].Gloss[id].Value, Is.EqualTo("glossa andorrana"));
			Assert.That(lfEntry.Senses[0].Gloss[namesakeId].Value, Is.EqualTo("glossa de l'homonima"));
			lfEntry.Senses[0].Gloss[id] = LfStringField.FromString("glossa andorrana, corregida a LF");
			lfEntry.Senses[0].Gloss[namesakeId] = LfStringField.FromString("glossa de l'homonima, corregida a LF");
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			var restores = new[] { AddLfInputSystem(id), AddLfInputSystem(namesakeId) };
			try
			{
				SutMongoToLcm.Run(_lfProj);
				SutLcmToMongo.Run(_lfProj);

				// Verify: neither writing system took the other's place or its text
				Assert.That(real.Handle, Is.EqualTo(realHandle));
				Assert.That(namesake.Handle, Is.EqualTo(namesakeHandle));
				Assert.That(wsManager.GetWsFromStr(id), Is.EqualTo(realHandle));
				Assert.That(wsManager.GetWsFromStr(namesakeId), Is.EqualTo(namesakeHandle));
				Assert.That(lcmEntry.SensesOS[0].Gloss.get_String(realHandle).Text,
					Is.EqualTo("glossa andorrana, corregida a LF"));
				Assert.That(lcmEntry.SensesOS[0].Gloss.get_String(namesakeHandle).Text,
					Is.EqualTo("glossa de l'homonima, corregida a LF"));
				lfEntry = _conn.GetLfLexEntryByGuid(_lfProj, entryGuid);
				Assert.That(lfEntry.Senses[0].Gloss[id].Value, Is.EqualTo("glossa andorrana, corregida a LF"));
				Assert.That(lfEntry.Senses[0].Gloss[namesakeId].Value, Is.EqualTo("glossa de l'homonima, corregida a LF"));
				Assert.That(_conn.GetInputSystems(_lfProj).Keys, Is.SupersetOf(new[] { id, namesakeId }));
			}
			finally
			{
				foreach (Action restore in restores)
					restore();
			}
		}

		/// <summary>
		/// LF's editor matches a field's input systems against an entry's keys exactly, so the two
		/// must be spelled alike. The keys are spelled by Id; the input systems were spelled by
		/// LanguageTag, so brb-flex-2022's Khmer text, keyed "km-Khmr-KH" under an input system
		/// called "km-KH", showed as empty.
		/// </summary>
		[Test]
		public void LcmToMongo_WritingSystemWhoseIdIsNotItsLanguageTag_IsSpelledByIdEverywhere()
		{
			// Setup: an analysis writing system held under a non-canonical Id, with a gloss in it.
			const string id = "nl-Latn-BE";
			const string gloss = "Nederlandse glos";
			CoreWritingSystemDefinition ws = AddWritingSystemWithId(id, "nl-BE");
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", _cache.ActionHandlerAccessor, () =>
			{
				_cache.LanguageProject.AddToCurrentAnalysisWritingSystems(ws);
				var lcmEntry = (ILexEntry)_cache.ServiceLocator.GetObject(entryGuid);
				lcmEntry.SensesOS[0].Gloss.set_String(ws.Handle, gloss);
			});

			// Exercise
			SutLcmToMongo.Run(_lfProj);

			// Verify
			Dictionary<string, LfInputSystemRecord> inputSystems = _conn.GetInputSystems(_lfProj);
			Assert.That(inputSystems.Keys, Contains.Item(id));
			Assert.That(inputSystems[id].Tag, Is.EqualTo(id));
			Assert.That(inputSystems.Keys, Does.Not.Contain("nl-BE"));
			LfLexEntry lfEntry = _conn.GetLfLexEntryByGuid(_lfProj, entryGuid);
			Assert.That(lfEntry.Senses[0].Gloss.Keys, Contains.Item(id),
				"the multitext key and the input system should be spelled alike");
			Assert.That(lfEntry.Senses[0].Gloss[id].Value, Is.EqualTo(gloss));
			Assert.That(_conn.LastAnalysisWss, Contains.Item(id));
			Assert.That(_conn.LastAnalysisWss, Does.Not.Contain("nl-BE"));
			var customFieldTags = _conn.GetCustomFieldConfig(_lfProj).Values.OfType<LfConfigMultiText>()
				.SelectMany(field => field.InputSystems).ToList();
			Assert.That(customFieldTags, Contains.Item(id), "a custom field offering every analysis writing system");
			Assert.That(customFieldTags, Does.Not.Contain("nl-BE"));
		}

		/// <summary>
		/// testlangproj's current vernacular list names "qaa-Zxxx-x-kal-AUDIO", and LCM holds the
		/// writing system as "qaa-Zxxx-x-kal-audio". It is in the vernacular list LF is given, so its
		/// input system must be flagged vernacular too; the list's own Contains compares Ids exactly.
		/// </summary>
		[Test]
		public void LcmToMongo_ListSpellingAnIdInAnotherCase_FlagsTheInputSystemAsInTheList()
		{
			Assert.That(_cache.LanguageProject.CurVernWss, Does.Contain(MixedCaseTag), "precondition");

			SutLcmToMongo.Run(_lfProj);

			Assert.That(_conn.LastVernacularWss, Contains.Item(MixedCaseId));
			Assert.That(_conn.GetInputSystems(_lfProj)[MixedCaseId].VernacularWS, Is.True);
		}

		[Test]
		public void LcmToMongo_PronunciationListNamingAWritingSystemLcmDoesNotHave_LeavesItOut()
		{
			// Setup: a pronunciation list naming an Id LCM has no writing system for. (The same in
			// the vernacular list would still fail, inside liblcm's own GetWritingSystemList.)
			string vernacularId = _cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", _cache.ActionHandlerAccessor, () =>
				_cache.LanguageProject.CurPronunWss = vernacularId + " zzz-x-nonesuch");

			// Exercise
			SutLcmToMongo.Run(_lfProj);

			// Verify: the list LF is given keeps the writing system LCM has and leaves out the other
			Assert.That(_conn.LastPronunciationWss, Is.EqualTo(new[] { vernacularId }));
		}

		[Test]
		public void WsIdFromLfTag_UnknownOrEmptyTag_IsZero()
		{
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, "zzz-x-nonesuch"), Is.EqualTo(0));
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, ""), Is.EqualTo(0));
			Assert.That(LanguageTags.WsIdFromLfTag(_cache.WritingSystemFactory, null), Is.EqualTo(0));
		}

		[Test]
		public void BestStringAndWsId_NonCanonicalKey_StillMatchesThePreferredWritingSystem()
		{
			// The lexicon's single-string fields compared LF's keys with ws.Id, so "fr-Latn" never
			// matched French and the English value was chosen although French comes first.
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");
			var multiText = new LfMultiText {
				{ "en", LfStringField.FromString("English") },
				{ "fr-Latn", LfStringField.FromString("Français") },
			};

			KeyValuePair<int, string> best = multiText.BestStringAndWsId(new[] { wsFr, _wsEn }, _cache.WritingSystemFactory);

			Assert.That(best.Key, Is.EqualTo(wsFr));
			Assert.That(best.Value, Is.EqualTo("Français"));
		}

		[Test]
		public void BestStringAndWsId_FallsBackToTheFirstValueInAKnownWritingSystem()
		{
			// An unidentified key comes first, but a value LCM has no writing system for is no use.
			var multiText = new LfMultiText {
				{ "zzz-x-nonesuch", LfStringField.FromString("Unplaceable") },
				{ "en", LfStringField.FromString("English") },
			};

			KeyValuePair<int, string> best = multiText.BestStringAndWsId(new int[0], _cache.WritingSystemFactory);

			Assert.That(best.Key, Is.EqualTo(_wsEn));
			Assert.That(best.Value, Is.EqualTo("English"));
		}

		/// <summary>
		/// A single-string field chooses between two keys naming one writing system as a multitext
		/// field does: the one spelled as the config spells it. Here the export's hidden value is
		/// under the Id and the LF user's under the LanguageTag, which the config uses.
		/// </summary>
		[Test]
		public void BestStringAndWsId_TwoKeysForOneWritingSystem_TakesTheConfigsSpelling()
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId("pl-Latn-PL", "pl-PL");
			var multiText = new LfMultiText {
				{ "pl-Latn-PL", LfStringField.FromString("the export's value") },
				{ "pl-PL", LfStringField.FromString("the user's edit") },
			};
			var configuredTags = new HashSet<string>(StringComparer.Ordinal) { "pl-PL" };

			KeyValuePair<int, string> best = multiText.BestStringAndWsId(new[] { ws.Handle }, _cache.WritingSystemFactory, configuredTags);

			Assert.That(best.Key, Is.EqualTo(ws.Handle));
			Assert.That(best.Value, Is.EqualTo("the user's edit"));
		}

		/// <summary>
		/// The key is chosen before empty values are left out: a value the LF user has cleared,
		/// under the config's spelling, is not replaced by the export's hidden one.
		/// </summary>
		[Test]
		public void BestStringAndWsId_ValueClearedUnderTheConfigsSpelling_IsNotReplacedByTheHiddenOne()
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId("cs-Latn-CZ", "cs-CZ");
			var multiText = new LfMultiText {
				{ "cs-Latn-CZ", LfStringField.FromString("the export's value") },
				{ "cs-CZ", LfStringField.FromString("") },
			};
			var configuredTags = new HashSet<string>(StringComparer.Ordinal) { "cs-CZ" };

			KeyValuePair<int, string> best = multiText.BestStringAndWsId(new[] { ws.Handle }, _cache.WritingSystemFactory, configuredTags);

			Assert.That(best.Value, Is.Null, "nothing should be placed for the cleared writing system");
		}

		[Test]
		public void KeysPassedOver_NamesEachKeyWithTextLeftOutAndWhy()
		{
			CoreWritingSystemDefinition ws = AddWritingSystemWithId("nl-Latn-SR", "nl-SR");
			var multiText = new LfMultiText {
				{ "nl-Latn-SR", LfStringField.FromString("the export's value") },
				{ "nl-SR", LfStringField.FromString("") },
				{ "zzz-x-nonesuch", LfStringField.FromString("unplaceable") },
				{ "en", LfStringField.FromString("placed") },
				{ "fr", LfStringField.FromString("") },
			};
			var configuredTags = new HashSet<string>(StringComparer.Ordinal) { "nl-SR", "en", "fr" };

			var passedOver = multiText.KeysPassedOver(_cache.WritingSystemFactory, configuredTags);

			Assert.That(passedOver, Is.EquivalentTo(new[] {
				new KeyValuePair<string, string>("nl-Latn-SR", "nl-SR"),
				new KeyValuePair<string, string>("zzz-x-nonesuch", null) }));
		}

		[Test]
		public void SpanStrToTsString_NonCanonicalSpanLang_UsesTheCanonicalWritingSystem()
		{
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");

			var tss = ConvertMongoToLcmTsStrings.SpanStrToTsString(
				"English <span lang=\"fr-Latn\">français</span>", _wsEn, _cache.WritingSystemFactory);

			Assert.That(tss.RunCount, Is.EqualTo(2));
			Assert.That(tss.get_WritingSystem(1), Is.EqualTo(wsFr));
		}

		/// <summary>
		/// A span whose lang names no writing system LCM has used to make the string build throw,
		/// abandoning the entry half-written. Its text is now kept in the string's own writing
		/// system, and the lang reported.
		/// </summary>
		[Test]
		public void SpanStrToTsString_SpanLangNamingNoWritingSystem_KeepsTheTextInTheMainWritingSystem()
		{
			var tss = ConvertMongoToLcmTsStrings.SpanStrToTsString(
				"English <span lang=\"zzz-x-nonesuch\">unplaceable</span>", _wsEn, _cache.WritingSystemFactory);

			Assert.That(tss.Text, Is.EqualTo("English unplaceable"));
			for (int run = 0; run < tss.RunCount; run++)
				Assert.That(tss.get_WritingSystem(run), Is.EqualTo(_wsEn));
			Assert.That(_env.Logger.GetMessages(), Does.Contain("span lang \"zzz-x-nonesuch\" names no writing system LCM has"));
		}

		[Test]
		public void LfWsToLcmWs_NewTagInBothAnchors_IsAddedAsBothVernacularAndAnalysis()
		{
			// Setup: a writing system LCM does not have yet, configured for the lexeme and for the
			// definition, as "en" so often is. Being vernacular must not stop it being analysis too,
			// or the definitions written in it would have no analysis writing system to go to.
			const string tag = "qaa-x-both";
			Assert.That(LcmWritingSystemIds(), Does.Not.Contain(tag), "testlangproj should not contain {0}", tag);
			var restores = new[] {
				AddLfInputSystem(tag),
				AddToConfigField(tag, "lexeme"),
				AddToConfigField(tag, "senses", "definition"),
			};

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				ILangProject langProj = _cache.LanguageProject;
				Assert.That(langProj.CurrentVernacularWritingSystems.Select(ws => ws.Id), Contains.Item(tag));
				Assert.That(langProj.CurrentAnalysisWritingSystems.Select(ws => ws.Id), Contains.Item(tag));
			}
			finally
			{
				foreach (Action restore in restores)
					restore();
			}
		}

		/// <summary>
		/// An input system LF spells non-canonically, while the config's fields spell it
		/// canonically: the lexeme field says "qaa-x-fresh" for the input system "qaa-x-qaa-fresh". The
		/// writing system is created under the canonical form, and must still get the vernacular
		/// role the lexeme field gives it; looked up under the input system's spelling only, it
		/// was not found, and was made analysis.
		/// </summary>
		[Test]
		public void LfWsToLcmWs_NewInputSystemSpelledOtherwiseThanInTheConfig_GetsTheConfigsRole()
		{
			const string inputSystemTag = "qaa-x-qaa-fresh", configTag = "qaa-x-fresh";
			Assert.That(LcmWritingSystemIds(), Does.Not.Contain(configTag), "testlangproj should not contain {0}", configTag);
			var restores = new[] {
				AddLfInputSystem(inputSystemTag),
				AddToConfigField(configTag, "lexeme"),
			};

			try
			{
				// Exercise
				SutMongoToLcm.Run(_lfProj);

				// Verify
				ILangProject langProj = _cache.LanguageProject;
				Assert.That(LcmWritingSystemIds(), Contains.Item(configTag));
				Assert.That(langProj.CurrentVernacularWritingSystems.Select(ws => ws.Id), Contains.Item(configTag));
				Assert.That(langProj.CurrentAnalysisWritingSystems.Select(ws => ws.Id), Does.Not.Contain(configTag));
			}
			finally
			{
				foreach (Action restore in restores)
					restore();
			}
		}

		/// <summary>
		/// Classifying a writing system counts the text in the whole lexicon, a second pass over
		/// Mongo. Only a writing system new to LCM is classified, so with none the lexicon is read
		/// once, for the entries themselves.
		/// </summary>
		[Test]
		public void MongoToLcm_NoInputSystemNewToLcm_ReadsTheLexiconOnce()
		{
			int readsBefore = _conn.LexiconReads;

			SutMongoToLcm.Run(_lfProj);

			Assert.That(_conn.LexiconReads - readsBefore, Is.EqualTo(1));
		}

		/// <summary>
		/// With writing systems new to LCM, the lexicon is counted once more, and only once however
		/// many there are: the classification is worked out for the first and kept for the rest.
		/// </summary>
		[Test]
		public void MongoToLcm_InputSystemsNewToLcm_CountTheLexiconOnceMore()
		{
			var tags = new[] { "qaa-x-new", "qaa-x-newtoo" };
			var restores = new List<Action>();
			foreach (string tag in tags)
			{
				Assert.That(LcmWritingSystemIds(), Does.Not.Contain(tag), "testlangproj should not contain {0}", tag);
				restores.Add(AddLfInputSystem(tag));
			}
			try
			{
				int readsBefore = _conn.LexiconReads;

				SutMongoToLcm.Run(_lfProj);

				Assert.That(LcmWritingSystemIds(), Is.SupersetOf(tags), "precondition: both should have been created");
				Assert.That(_conn.LexiconReads - readsBefore, Is.EqualTo(2));
			}
			finally
			{
				foreach (Action restore in restores)
					restore();
			}
		}
	}
}
