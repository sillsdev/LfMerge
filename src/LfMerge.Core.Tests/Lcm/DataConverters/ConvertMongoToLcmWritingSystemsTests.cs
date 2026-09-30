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
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;

namespace LfMerge.Core.Tests.Lcm.DataConverters
{
	/// <summary>
	/// Language Forge stores a writing system tag exactly as it was typed, but LCM canonicalizes a
	/// tag when it creates a writing system and thereafter knows that writing system only by the
	/// canonical id. Every LCM lookup -- WritingSystemManager.TryGet, GetWsFromStr -- matches that id
	/// literally, so a tag that is not already canonical is never found.
	///
	/// testlangproj already contains "qaa-x-kal" and "qaa-Zxxx-x-kal-audio", so a non-canonical LF
	/// spelling of either exercises the mismatch without adding anything to the shared fixture project.
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
		/// Adds one input system to the memoized mock project record, and returns an action that puts
		/// the record back as it was. The double memoizes per project code, so without the restore the
		/// addition would leak into every later test in this fixture.
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
		/// config is memoized with the record, so the change must not outlive the test.
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
			// Setup: a definition keyed by the non-canonical tag. LfMultiText resolves each key with
			// GetWsFromStr and skips anything that returns 0, so an unresolved key silently drops the
			// value -- and the clearing pass then blanks whatever LCM had for that field.
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
			// input systems -- which LfMerge exports by LanguageTag -- call it "hi-IN". GetOrSet looks
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

		[Test]
		public void SpanStrToTsString_NonCanonicalSpanLang_UsesTheCanonicalWritingSystem()
		{
			int wsFr = _cache.WritingSystemFactory.GetWsFromStr("fr");

			var tss = ConvertMongoToLcmTsStrings.SpanStrToTsString(
				"English <span lang=\"fr-Latn\">français</span>", _wsEn, _cache.WritingSystemFactory);

			Assert.That(tss.RunCount, Is.EqualTo(2));
			Assert.That(tss.get_WritingSystem(1), Is.EqualTo(wsFr));
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
	}
}
