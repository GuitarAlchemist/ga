namespace GA.Core.Extensions;

[PublicAPI]
public static class ValueObjectExtensions
{
    public static ImmutableArray<int> ToValueArray<T>(this IEnumerable<T> items) 
        where T : IRangeValueObject<T>, new()
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Select(item => item.Value).ToImmutableArray();
    }

    public static ImmutableList<int> ToValueList<T>(this IEnumerable<T> items) 
        where T : IRangeValueObject<T>, new()
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Select(item => item.Value).ToImmutableList();
    }
}

