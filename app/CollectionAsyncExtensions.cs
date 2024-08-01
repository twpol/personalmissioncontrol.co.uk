using System;
using System.Collections.Generic;

namespace app
{
    public static class CollectionAsyncExtensions
    {
        public static async IAsyncEnumerable<TResult> SelectManyAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            foreach (var sourceItem in source)
            {
                await foreach (var resultItem in selector(sourceItem))
                    yield return resultItem;
            }
        }
    }
}
