namespace GA.Core.Extensions;

using Collections;

public static class PrintCollectionExtensions
{
    [PublicAPI]
    public static PrintableEnumerable<T> AsPrintable<T>(this IEnumerable<T> items)
        where T : notnull
    {
        if (items is PrintableEnumerable<T> printable) return printable;
        return new(items);
    }

    [PublicAPI]
    public static PrintableReadOnlyCollection<T> AsPrintable<T>(
        this IReadOnlyCollection<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null)
           where T : notnull
    {
        if (items is PrintableReadOnlyCollection<T> printable) return printable;
        return new(items, itemFormat, itemFormatProvider);
    }

    [PublicAPI]
    public static PrintableReadOnlySet<T> AsPrintable<T>(this IReadOnlySet<T> items)
        where T : notnull
    {
        if (items is PrintableReadOnlySet<T> printable) return printable;
        return new(items);
    }

}