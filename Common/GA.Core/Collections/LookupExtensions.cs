namespace GA.Core.Collections;

[PublicAPI]
public static class LookupExtensions
{
    extension<TKey, TElement>(ILookup<TKey, TElement> lookup) where TKey : notnull
    {
        public ImmutableSortedDictionary<TKey, int> GetCounts()
        {
            ArgumentNullException.ThrowIfNull(lookup);

            var dictBuilder = ImmutableSortedDictionary.CreateBuilder<TKey, int>();
            foreach (var grouping in lookup)
            {
                var norm = grouping.Key;
                dictBuilder.Add(norm, grouping.Count());
            }

            return dictBuilder.ToImmutable();
        }
    }
}
