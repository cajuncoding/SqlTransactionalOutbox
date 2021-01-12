using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers.CustomExtensions
{

    public static class EnumerableCustomExtensions
    {
        /// <summary>
        /// Borrowed from the various algorithms outlined at StackOverflow here:
        /// https://stackoverflow.com/a/25942344/7293142
        ///
        /// NOTE: This approach is simple, and performs well enough in our use case where full Lazy processing of
        /// the source IEnumerable is not an issue.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> items, int size)
        {
            T[] array = items as T[] ?? items.ToArray();
            for (int i = 0; i < array.Length; i += size)
            {
                T[] chunk = new T[Math.Min(size, array.Length - i)];
                Array.Copy(array, i, chunk, 0, chunk.Length);
                yield return chunk;
            }
        }
    }
}
