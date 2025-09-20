namespace GA.Core.Extensions;

using Collections;

[PublicAPI]
public static class CollectionExtensions
{
    public static Lazy<ImmutableList<T>> ToLazyImmutableList<T>(this IEnumerable<T> items) => new(items.ToImmutableList);

    public static LazyCollection<T> ToLazyCollection<T>(this IEnumerable<T> items) 
        where T : class 
            => new(items);

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
    public class RotatedCollection<T>(IReadOnlyCollection<T> items, int shift = 0) : IReadOnlyList<T>
    {
        private readonly ImmutableList<T> _items = items.ToImmutableList();

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