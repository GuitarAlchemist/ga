namespace GA.Core;

using System.Collections;

public static class CollectionExtensions
{
    public static IEnumerable<T> AsPrintable<T>(this IEnumerable<T> items) => new PrintableEnumerable<T>(items);
    public static IReadOnlyCollection<T> AsPrintable<T>(this IReadOnlyCollection<T> items) => new PrintableReadOnlyCollection<T>(items);

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
    {
        private readonly IReadOnlyCollection<T> _items;

        public PrintableReadOnlyCollection(IReadOnlyCollection<T> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => string.Join(" ", _items);
        public int Count => _items.Count;
    }
}
