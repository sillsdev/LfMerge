// Copyright (c) 2017 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Core.MongoConnector
{
	/// Reimplementations of some PHP functions that turn out to be useful in the MongoConnector class
	public static class PseudoPhp
	{
		/// PHP's uniqid() is used in identifying comment replies, so we need to sort-of reimplement it here.
		/// But since uniqid() really just returns the current Unix timestamp in a specific string format,
		/// I changed its name to reflect what it *really* does.
		///
		/// Note: this function is deterministic, so that it will be easily testable. In most common usage
		/// you'll want to pass DateTime.UtcNow as input here.
		public static string NonUniqueIdFromDateTime(DateTime timestamp)
		{
			TimeSpan sinceEpoch = timestamp - MagicValues.UnixEpoch;
			long seconds = sinceEpoch.Ticks / TimeSpan.TicksPerSecond;
			long ticks = sinceEpoch.Ticks % TimeSpan.TicksPerSecond;
			long microseconds = (ticks / 10) % 0x100000;  // PHP only uses five hex digits of the microseconds value
			return seconds.ToString("x8") + microseconds.ToString("x5");
		}

		internal static string LastUniqueId = "0000000000000";

		public static string UniqueIdFromDateTime(DateTime timestamp)
		{
			string result = NonUniqueIdFromDateTime(timestamp);
			while (String.CompareOrdinal(result, LastUniqueId) <= 0)
			{
				timestamp = timestamp.AddTicks(10);  // 10 ticks = 1 microsecond
				result = NonUniqueIdFromDateTime(timestamp);
			}
			LastUniqueId = result;
			return result;
		}
	}
}
