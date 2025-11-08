namespace GA.Core.Collections;

[PublicAPI]
public static class LookupExtensions
{
    public static ImmutableSortedDictionary<TKey, int> GetCounts<TKey, TElement>(this ILookup<TKey, TElement> lookup)
        where TKey : notnull
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
