namespace GA.Core.Extensions;

using Collections;

[PublicAPI]
public static class CollectionExtensions
{
    extension<T>(IEnumerable<T> items)
    {
        public Lazy<ImmutableList<T>> ToLazyImmutableList() => new(items.ToImmutableList);

        public ReadOnlySpan<T> ToSpan() => items is T[] arr ? arr : [.. items];
    }

    extension<T>(IEnumerable<T> items) where T : class
    {
        public LazyCollection<T> ToLazyCollection() => new(items);
    }

    extension<T>(IReadOnlyCollection<T> items)
    {
        /// <summary>
        ///     Rotate the items of a collection
        /// </summary>
        /// <param name="shift">The shift.</param>
        /// <returns>The rotated <see cref="IReadOnlyList{T}" />.</returns>
        public IReadOnlyList<T> Rotate(int shift = 0) => new RotatedCollection<T>(items, shift);
    }

    [PublicAPI]
    public class RotatedCollection<T>(IReadOnlyCollection<T> items, int shift = 0) : IReadOnlyList<T>
    {
        private readonly ImmutableList<T> _items = [.. items];

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var index = (i + shift) % Count;
                yield return _items[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _items.Count;

        public T this[int aIndex]
        {
            get
            {
                var index = (aIndex + shift) % Count;
                return _items[index];
            }
        }

        public override string ToString() => string.Join(" ", this);
    }
}
