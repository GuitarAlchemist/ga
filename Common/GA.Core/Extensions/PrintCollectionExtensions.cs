namespace GA.Core.Extensions;

using Collections;

public static class PrintCollectionExtensions
{
    extension<T>(IEnumerable<T> items) where T : notnull
    {
        [PublicAPI]
        public PrintableEnumerable<T> AsPrintable(string? itemSeparator = " ")
        {
            if (items is PrintableEnumerable<T> printable)
            {
                return printable;
            }

            return new(items, itemSeparator);
        }
    }

    extension<T>(IReadOnlyCollection<T> items) where T : notnull
    {
        [PublicAPI]
        public PrintableReadOnlyCollection<T> AsPrintable(
            string? itemFormat = null,
            IFormatProvider? itemFormatProvider = null,
            string? itemSeparator = " ")
        {
            if (items is PrintableReadOnlyCollection<T> printable)
            {
                return printable;
            }

            return new(items, itemFormat, itemFormatProvider, itemSeparator);
        }
    }

    extension<T>(IImmutableSet<T> items) where T : notnull
    {
        [PublicAPI]
        public PrintableReadOnlySet<T> AsPrintable(string? itemSeparator = " ")
        {
            if (items is PrintableReadOnlySet<T> printable)
            {
                return printable;
            }

            return new(items, itemSeparator);
        }
    }
}
