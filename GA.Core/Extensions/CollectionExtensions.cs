namespace GA.Core.Extensions;

[PublicAPI]
public static class CollectionExtensions
{
    /// <summary>
    /// Rotate the items of a collection
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The source items collection.</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The rotated <see cref="IReadOnlyList{T}"/>.</returns>
    public static IReadOnlyList<T> Rotate<T>(
        this IReadOnlyCollection<T> items,
        int shift = 0)
    {
        return new RotatedCollection<T>(items, shift);
    }

    [PublicAPI]
    public class RotatedCollection<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyCollection<T> _items;
        private readonly int _shift;

        public RotatedCollection(
            IReadOnlyCollection<T> items,
            int shift = 0)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _shift = shift;
        }

        public IEnumerator<T> GetEnumerator()
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < _items.Count; i++)
            {
                var index = (i + _shift) % Count;
                yield return _items.ElementAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _items.Count;

        public T this[int aIndex]
        {
            get
            {
                var index = (aIndex + _shift) % Count;
                return _items.ElementAt(index);
            }
        }

        public override string ToString() => string.Join(" ", this);
    }
}