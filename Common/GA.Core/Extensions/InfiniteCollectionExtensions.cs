namespace GA.Core.Extensions;

[PublicAPI]
public static class InfiniteCollectionExtensions
{
    extension<T>(IReadOnlyCollection<T> items)
    {
        /// <summary>
        ///     Create an infinite collection.
        /// </summary>
        /// <param name="skip">Number of items to skip.</param>
        /// <returns>The <see cref="InfiniteCollection{T}" />.</returns>
        public InfiniteCollection<T> ToInfinite(int? skip = null) => new(items, skip);
    }

    [PublicAPI]
    public class InfiniteCollection<T> : IEnumerable<T>
    {
        private readonly IReadOnlyCollection<T> _items;
        private Index _index = Index.Start;

        public InfiniteCollection(
            IReadOnlyCollection<T> items,
            int? skip = null)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (skip.HasValue)
            {
                IncrementIndex(skip.Value);
            }
        }

        public int CycleItemCount => _items.Count;
        public Index Index => _index;

        public IEnumerator<T> GetEnumerator()
        {
            yield return _items.ElementAt(_index);
            IncrementIndex();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"Infinite: {string.Join(" ", this.Take(CycleItemCount))}";

        private void IncrementIndex(int count = 1) => _index = (_index.Value + count) % CycleItemCount;
    }
}
