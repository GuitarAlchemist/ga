namespace GA.Business.Core.Extensions;

using System.Collections.Immutable;

public static class ValueObjectExtensions
{
    public static ImmutableList<int> ToValues<T>(this IEnumerable<T> items) 
        where T : struct, IValueObject<T>
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        return items.Select(item => item.Value).ToImmutableList();
    }
}

