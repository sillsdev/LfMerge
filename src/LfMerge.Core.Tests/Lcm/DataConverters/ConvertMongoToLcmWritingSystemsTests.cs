using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.MongoConnector;
using LfMergeBridge.LfMergeModel;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.LCModel;

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
	}
}
