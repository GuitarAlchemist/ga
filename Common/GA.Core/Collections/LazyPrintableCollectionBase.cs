namespace GA.Core.Collections;

using Extensions;

/// <summary>
/// Base class for lazy-printable read-only collections
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class LazyPrintableCollectionBase<T> : IReadOnlyCollection<T>
    where T : notnull
{
    private readonly IReadOnlyCollection<T> _items;
    private readonly Lazy<PrintableReadOnlyCollection<T>> _lazyPrintableCollection;

    protected LazyPrintableCollectionBase(
        IReadOnlyCollection<T> items,
        string? itemSeparator = " ")
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _lazyPrintableCollection = new(() => items.AsPrintable());
    }

    public IEnumerator<T> GetEnumerator() =>_items.GetEnumerator();
    public override string ToString() => _lazyPrintableCollection.Value.ToString();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    public int Count => _items.Count;
}