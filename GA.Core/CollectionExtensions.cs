using System.Collections;
using JetBrains.Annotations;

namespace GA.Core;

[PublicAPI]
public static class CollectionExtensions
{
    /// <summary>
    /// Rotate the items of a collection
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The source items collection.</param>
    /// <param name="start">The start <see cref="Index"/>.</param>
    /// <returns>The rotated <see cref="IReadOnlyCollection{T}"/>.</returns>
    public static IReadOnlyCollection<T> Rotate<T>(
        this IReadOnlyCollection<T> items,
        Index start = default)
    {
        return new RotatedCollection<T>(items, start);
    }

    [PublicAPI]
    public class RotatedCollection<T> : IReadOnlyCollection<T>
    {
        private readonly IReadOnlyCollection<T> _items;
        private Index _index = Index.Start;

        public RotatedCollection(
            IReadOnlyCollection<T> items,
            Index start = default)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (start.Value > 0) IncrementIndex(start.Value);
        }

        public int Count => _items.Count;
        public Index Index => _index;

        public IEnumerator<T> GetEnumerator()
        {
            yield return _items.ElementAt(_index);
            IncrementIndex();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => string.Join(" ", this);

        private void IncrementIndex(int count = 1) => _index = (_index.Value + count) % Count;
    }
}