namespace GA.Core;

using JetBrains.Annotations;

using System.Collections;

public static class PrintCollectionExtensions
{
    [PublicAPI]
    public static IEnumerable<T> AsPrintable<T>(this IEnumerable<T> items)
        where T: notnull
    {
        return new PrintableEnumerable<T>(items);
    }


    [PublicAPI]
    public static IReadOnlyCollection<T> AsPrintable<T>(
        this IReadOnlyCollection<T> items, 
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null)
           where T: notnull
    {
        return new PrintableReadOnlyCollection<T>(items, itemFormat, itemFormatProvider);
    }

    [PublicAPI]
    public static IReadOnlySet<T> AsPrintable<T>(this IReadOnlySet<T> items)
        where T: notnull
    {
        return new PrintableReadOnlySet<T>(items);
    }
    
    private class PrintableEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _items;

        public PrintableEnumerable(IEnumerable<T> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => string.Join(" ", _items);
    }

    private class PrintableReadOnlyCollection<T> : IReadOnlyCollection<T>
        where T: notnull
    {
        private readonly IReadOnlyCollection<T> _items;
        private readonly string? _itemFormat;
        private readonly IFormatProvider? _itemFormatProvider;

        public PrintableReadOnlyCollection(
            IReadOnlyCollection<T> items,
            string? itemFormat = null,
            IFormatProvider? itemFormatProvider = null)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _itemFormat = itemFormat;
            _itemFormatProvider = itemFormatProvider;
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return string.Join(" ", _items.Select(PrintItem()));

            Func<T, string?> PrintItem()
            {
                return arg =>
                {
                    ArgumentNullException.ThrowIfNull(arg);

                    if (arg is IFormattable f) return f.ToString(_itemFormat, _itemFormatProvider);
                    return arg.ToString();
                };
            }
        }
        public int Count => _items.Count;
    }

    private class PrintableReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly IReadOnlySet<T> _items;

        public PrintableReadOnlySet(IReadOnlySet<T> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => _items.Count;

        public bool Contains(T item)=> _items.Contains(item);
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();

        public override string ToString() => string.Join(" ", _items);
    }
}
