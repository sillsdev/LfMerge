// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections;
using System.Collections.Generic;

namespace LfMerge
{
	/// <summary>
	/// Bidirectional dictionary for a one-to-one mapping between two data collections, that can be looked up in either direction.
	/// </summary>
	// TODO: Needs unit tests.
	public class BidirectionalDictionary<TFirst, TSecond> : IDictionary<TFirst, TSecond>
	{
		IDictionary<TFirst, TSecond> forwardDict = new Dictionary<TFirst, TSecond>();
		IDictionary<TSecond, TFirst> reverseDict = new Dictionary<TSecond, TFirst>();

		public BidirectionalDictionary()
		{
		}

		public void Add(TFirst first, TSecond second)
		{
			forwardDict.Add(first, second);
			reverseDict.Add(second, first);
		}

		public void Add(KeyValuePair<TFirst, TSecond> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			forwardDict.Clear();
			reverseDict.Clear();
		}

		public bool Contains(KeyValuePair<TFirst, TSecond> item) {
			return forwardDict.Contains(item);
		}

		public bool ContainsKey(TFirst key)
		{
			return forwardDict.ContainsKey(key);
		}

		public bool ContainsValue(TSecond value)
		{
			return reverseDict.ContainsKey(value); // Faster than forwardDict.ContainsValue
		}

		public void CopyTo(KeyValuePair<TFirst, TSecond>[] array, int arrayIndex)
		{
			forwardDict.CopyTo(array, arrayIndex);
		}

		public int Count { get { return forwardDict.Count; } }

		public TSecond GetByFirst(TFirst key)
		{
			return forwardDict[key];
		}

		public TFirst GetBySecond(TSecond key)
		{
			return reverseDict[key];
		}

		public IEnumerator<KeyValuePair<TFirst, TSecond>> GetEnumerator()
		{
			return forwardDict.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool IsReadOnly { get { return forwardDict.IsReadOnly; } }

		public TSecond this[TFirst key]
		{
			get { return this.GetByFirst(key); }
			set { this.Add(key, value); }
		}

		public ICollection<TFirst> Keys { get { return forwardDict.Keys; } }
		public ICollection<TSecond> Values { get { return forwardDict.Values; } }

		public bool Remove(TFirst key)
		{
			return RemoveByFirst(key);
		}

		public bool Remove(KeyValuePair<TFirst, TSecond> item)
		{
			if (!forwardDict.Contains(item))
				return false;
			KeyValuePair<TSecond, TFirst> reversedItem = new KeyValuePair<TSecond, TFirst>(item.Value, item.Key);
			bool result1 = forwardDict.Remove(item);
			bool result2 = reverseDict.Remove(reversedItem);
			return result1 && result2;
		}

		public bool RemoveByFirst(TFirst first)
		{
			if (!forwardDict.ContainsKey(first))
				return false;
			TSecond second = forwardDict[first];
			bool result1 = forwardDict.Remove(first);
			bool result2 = reverseDict.Remove(second);
			return result1 && result2;
		}

		public bool RemoveBySecond(TSecond second)
		{
			if (!reverseDict.ContainsKey(second))
				return false;
			TFirst first = reverseDict[second];
			bool result1 = forwardDict.Remove(first);
			bool result2 = reverseDict.Remove(second);
			return result1 && result2;
		}

		public bool TryGetValue(TFirst key, out TSecond value)
		{
			return TryGetValueByFirst(key, out value);
		}

		public bool TryGetValueByFirst(TFirst key, out TSecond value)
		{
			return forwardDict.TryGetValue(key, out value);
		}

		public bool TryGetValueBySecond(TSecond key, out TFirst value)
		{
			return reverseDict.TryGetValue(key, out value);
		}

	}
}

