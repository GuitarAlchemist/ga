namespace GA.Core.Collections;

internal static class ValueObjectCache<T>
    where T : IRangeValueObject<T>
{
    // ReSharper disable StaticMemberInGenericType
    internal static readonly int Min = T.Min.Value;
    internal static readonly int Max = T.Max.Value;
    // Ensure _count is initialized BEFORE we create items/values
    private static readonly int _count = Max - Min + 1;
    internal static readonly T[] AllItems = CreateItems();
    internal static FrozenSet<T> ItemsSet { get; } = FrozenSet.Create<T>(AllItems);
    internal static readonly ImmutableArray<int> AllValues = CreateValues();
    internal static FrozenSet<int> ValuesSet { get; } = [..AllValues];
    internal static ReadOnlySpan<T> ItemsSpan => AllItems;
    internal static ReadOnlySpan<int> ValuesSpan => AllValues.AsSpan();
    // ReSharper restore StaticMemberInGenericType

    private static T[] CreateItems()
    {
        if (_count <= 0) return [];
        var array = new T[_count];
        for (var i = 0; i < _count; i++)
        {
            array[i] = T.FromValue(Min + i);
        }

        return array;
    }

    private static ImmutableArray<int> CreateValues()
    {
        if (_count <= 0) return [];
        var builder = ImmutableArray.CreateBuilder<int>(_count);
        for (var i = 0; i < _count; i++)
        {
            builder.Add(Min + i);
        }

        return builder.MoveToImmutable();
    }
}
