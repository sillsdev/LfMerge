// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using MongoDB.Driver;
using System.Collections.Generic;

namespace LfMerge
{
	public static class MongoCursorEnumerableExtensions
	{
		/// <summary>
		/// Turn an IAsyncCursor from Mongo into an IEnumerable. I'm shocked that this isn't in the MongoDB driver already.
		/// NOTE that this is not async, because you can't use "yield return" in an async function.
		/// </summary>
		/// <returns>An enumeration of the cursor's results.</returns>
		/// <param name="cursor">Cursor to enumerate over.</param>
		public static IEnumerable<TDocument> AsEnumerable<TDocument>(this IAsyncCursor<TDocument> cursor)
		{
			while (cursor.MoveNextAsync().Result)
				foreach (TDocument doc in cursor.Current) // IAsyncCursor returns results in batches
					yield return doc;
		}

		// This allows the following two ways of running a query to fetch all the documents from a given collection:
		/*
			// If fetching all the documents at once won't be too costly:
			List<BsonDocument> result = collection.Find<BsonDocument>(_ => true).ToListAsync().Result;
			foreach (BsonDocument item in result)
			{
				Console.WriteLine(item);
			}

			// If it is desirable to fetch documents in batches rather than all at once:
			IAsyncCursor<BsonDocument> result2 = collection.Find<BsonDocument>(_ => true).ToCursorAsync().Result;
			foreach (BsonDocument item in result2.AsEnumerable())
			{
				Console.WriteLine(item);
			}
		*/

	}
}

