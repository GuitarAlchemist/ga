namespace GA.Core.ValueObjects;

internal static class ValueObjectCache<T>
    where T : IRangeValueObject<T>
{
    private static T[] CreateItems()
    {
        if (_count <= 0)
        {
            return [];
        }

        var array = new T[_count];
        for (var i = 0; i < _count; i++)
        {
            array[i] = T.FromValue(Min + i);
        }

        return array;
    }

    private static ImmutableArray<int> CreateValues()
    {
        if (_count <= 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<int>(_count);
        for (var i = 0; i < _count; i++)
        {
            builder.Add(Min + i);
        }

        return builder.MoveToImmutable();
    }

    // ReSharper disable StaticMemberInGenericType
    internal static readonly int Min = T.Min.Value;

    internal static readonly int Max = T.Max.Value;

    // Ensure _count is initialized BEFORE we create items/values
    private static readonly int _count = Max - Min + 1;
    internal static readonly T[] AllItems = CreateItems();
    internal static readonly ImmutableArray<int> AllValues = CreateValues();
    internal static readonly IReadOnlyList<int> AllValuesList = AllValues;

    // The frozen sets are built on first use: nothing in GA reads them, and building both on every
    // type's first use cost 4,592 bytes for Str's 26 values.
    internal static FrozenSet<T> ItemsSet => FrozenSets.Items;
    internal static FrozenSet<int> ValuesSet => FrozenSets.Values;
    internal static ReadOnlySpan<T> ItemsSpan => AllItems;

    internal static ReadOnlySpan<int> ValuesSpan => AllValues.AsSpan();

    private static class FrozenSets
    {
        internal static readonly FrozenSet<T> Items = FrozenSet.Create<T>(AllItems);
        internal static readonly FrozenSet<int> Values = [.. AllValues];
    }
    // ReSharper restore StaticMemberInGenericType
}
