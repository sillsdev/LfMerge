// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using LfMerge;
using NUnit.Framework;

namespace LfMerge.Tests
{
	[TestFixture]
	public class BidirectionalDictionaryTests
	{
		private BidirectionalDictionary<string, int> Dict { get; set; }

		public BidirectionalDictionaryTests()
		{
		}

		[SetUp]
		public void Setup()
		{
			// Don't put these in constructor; constructor is only run once for all tests
			Dict = new BidirectionalDictionary<string, int>();
			Dict.Add("one", 1);
			Dict.Add("two", 2);
			Dict.Add("three", 3);
		}

		[Test]
		public void AddDoubleDuplicate_WillFail()
		{
			var exception = Assert.Throws<ArgumentException>(() => Dict.Add("one", 1));
			Assert.That(exception.Message, Is.StringMatching("An item with the same (key|value) has already been added"));
		}

		[Test]
		public void AddSingleDuplicate_InFirstPosition_WillFail()
		{
			var exception = Assert.Throws<ArgumentException>(() => Dict.Add("one", 4));
			Assert.That(exception.Message, Is.StringMatching("An item with the same key has already been added"));
		}

		[Test]
		public void AddSingleDuplicate_InSecondPosition_WillFail()
		{
			var exception = Assert.Throws<ArgumentException>(() => Dict.Add("four", 1));
			Assert.That(exception.Message, Is.StringMatching("An item with the same value has already been added"));
		}

		[Test]
		public void AddDoubleDuplicate_WillNotChangeDict()
		{
			Assert.Throws<ArgumentException>(() => Dict.Add("one", 1));
			Assert.That(Dict.Count, Is.EqualTo(3));
			string oneStr = Dict.GetBySecond(1);
			int oneInt = Dict.GetByFirst("one");
			Assert.That(oneStr, Is.EqualTo("one"));
			Assert.That(oneInt, Is.EqualTo(1));
			int fourInt = -9999;
			bool found = Dict.TryGetValueByFirst("four", out fourInt);
			Assert.That(found, Is.False);
			Assert.That(fourInt, Is.EqualTo(default(int)));
		}
			
		[Test]
		public void AddSingleDuplicate_InFirstPosition_WillNotChangeDict()
		{
			Assert.Throws<ArgumentException>(() => Dict.Add("four", 1));
			Assert.That(Dict.Count, Is.EqualTo(3));
			string oneStr = Dict.GetBySecond(1);
			int oneInt = Dict.GetByFirst("one");
			Assert.That(oneStr, Is.EqualTo("one"));
			Assert.That(oneInt, Is.EqualTo(1));
			int fourInt = -9999;
			bool found = Dict.TryGetValueByFirst("four", out fourInt);
			Assert.That(found, Is.False);
			Assert.That(fourInt, Is.EqualTo(default(int)));
		}

		[Test]
		public void AddSingleDuplicate_InSecondPosition_WillNotChangeDict()
		{
			Assert.Throws<ArgumentException>(() => Dict.Add("one", 4));
			Assert.That(Dict.Count, Is.EqualTo(3));
			string oneStr = Dict.GetBySecond(1);
			int oneInt = Dict.GetByFirst("one");
			Assert.That(oneStr, Is.EqualTo("one"));
			Assert.That(oneInt, Is.EqualTo(1));
			string fourStr = "**INVALID**";
			bool found = Dict.TryGetValueBySecond(4, out fourStr);
			Assert.That(found, Is.False);
			Assert.That(fourStr, Is.EqualTo(default(string)));
		}

		[Test]
		public void RemoveKeyValuePair_OfExistingItem_Succeeds()
		{
			bool success = ((ICollection<KeyValuePair<string, int>>)Dict).Remove(new KeyValuePair<string, int>("two", 2));
			Assert.That(success, Is.True);
		}

		[Test]
		public void RemoveKeyValuePair_OfExistingItem_ChangesCount()
		{
			Assert.That(Dict.Count, Is.EqualTo(3));
			((ICollection<KeyValuePair<string, int>>)Dict).Remove(new KeyValuePair<string, int>("two", 2));
			Assert.That(Dict.Count, Is.EqualTo(2));
		}


		[Test]
		public void RemoveKeyValuePair_OfNonexistentItem_Fails()
		{
			bool success = ((ICollection<KeyValuePair<string, int>>)Dict).Remove(new KeyValuePair<string, int>("zwei", 2));
			Assert.That(success, Is.False);
		}

		[Test]
		public void RemoveKeyValuePair_OfNonexistentItemByFirstValue_DoesNotChangeCount()
		{
			Assert.That(Dict.Count, Is.EqualTo(3));
			((ICollection<KeyValuePair<string, int>>)Dict).Remove(new KeyValuePair<string, int>("zwei", 2));
			Assert.That(Dict.Count, Is.EqualTo(3));
		}

		[Test]
		public void RemoveKeyValuePair_OfNonexistentItemBySecondValue_DoesNotChangeCount()
		{
			Assert.That(Dict.Count, Is.EqualTo(3));
			((ICollection<KeyValuePair<string, int>>)Dict).Remove(new KeyValuePair<string, int>("two", 20));
			Assert.That(Dict.Count, Is.EqualTo(3));
		}

		[Test]
		public void Keys_ReturnsAllThreeKeys()
		{
			List<string> keys = new List<string>(Dict.Keys);
			keys.Sort(StringComparer.InvariantCulture);
			Assert.That(keys, Is.EquivalentTo(new string[] { "one", "three", "two" }));
		}

		[Test]
		public void Keys_AfterDeletionByFirst_ReturnsTwoKeys()
		{
			Dict.RemoveByFirst("two");
			List<string> keys = new List<string>(Dict.Keys);
			keys.Sort(StringComparer.InvariantCulture);
			Assert.That(keys, Is.EquivalentTo(new string[] { "one", "three" }));
		}

		[Test]
		public void Keys_AfterDeletionBySecond_ReturnsTwoKeys()
		{
			Dict.RemoveBySecond(2);
			List<string> keys = new List<string>(Dict.Keys);
			keys.Sort(StringComparer.InvariantCulture);
			Assert.That(keys, Is.EquivalentTo(new string[] { "one", "three" }));
		}

		[Test]
		public void Keys_AfterInsertion_ReturnsFourKeys()
		{
			Dict.Add("four", 4);
			List<string> keys = new List<string>(Dict.Keys);
			keys.Sort(StringComparer.InvariantCulture);
			Assert.That(keys, Is.EquivalentTo(new string[] { "four", "one", "three", "two" }));
		}

		[Test]
		public void Values_ReturnsAllThreeValues()
		{
			List<int> values = new List<int>(Dict.Values);
			values.Sort();
			Assert.That(values, Is.EquivalentTo(new int[] { 1, 2, 3 }));
		}

		[Test]
		public void Values_AfterDeletionByFirst_ReturnsTwoValues()
		{
			Dict.RemoveByFirst("two");
			List<int> keys = new List<int>(Dict.Values);
			keys.Sort();
			Assert.That(keys, Is.EquivalentTo(new int[] { 1, 3 }));
		}

		[Test]
		public void Values_AfterDeletionBySecond_ReturnsTwoValues()
		{
			Dict.RemoveBySecond(2);
			List<int> keys = new List<int>(Dict.Values);
			keys.Sort();
			Assert.That(keys, Is.EquivalentTo(new int[] { 1, 3 }));
		}

		[Test]
		public void Values_AfterInsertion_ReturnsFourValues()
		{
			Dict.Add("four", 4);
			List<int> keys = new List<int>(Dict.Values);
			keys.Sort();
			Assert.That(keys, Is.EquivalentTo(new int[] { 1, 2, 3, 4 }));
		}
	}
}

