namespace GA.Core.Extensions;

[PublicAPI]
public static class ValueObjectExtensions
{
    public static ImmutableArray<int> ToValueArray<T>(this IEnumerable<T> items) 
        where T : IValueObject<T>, new()
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        return items.Select(item => item.Value).ToImmutableArray();
    }

    public static ImmutableList<int> ToValueList<T>(this IEnumerable<T> items) 
        where T : IValueObject<T>, new()
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        return items.Select(item => item.Value).ToImmutableList();
    }

    public static IEnumerable<T> ToNormalized<T>(this IEnumerable<T> items) 
        where T : struct, IValueObject<T>
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items is not IReadOnlyCollection<T> collection) collection = items.ToImmutableArray();

        var minItem = collection.Min();
        return collection.Select(item => T.FromValue(item.Value - minItem.Value));
    }

    public static IEnumerable<T> ToNormalizedArray<T>(this IEnumerable<T> items) 
        where T : struct, IValueObject<T>
            => ToNormalized<T>(items).ToImmutableArray();
}

