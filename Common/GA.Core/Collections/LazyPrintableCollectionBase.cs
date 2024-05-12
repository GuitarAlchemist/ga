namespace GA.Core.Collections;

using Extensions;

/// <summary>
/// Base class for lazy-printable read-only collections
/// </summary>
/// <typeparam name="T">The item type</typeparam>
public abstract class LazyPrintableCollectionBase<T>(IReadOnlyCollection<T> items) : IReadOnlyCollection<T>
    where T : notnull
{
    #region IReadOnlyCollection Members

    public IEnumerator<T> GetEnumerator() =>_items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    public int Count => _items.Count;

    #endregion
    
    private readonly IReadOnlyCollection<T> _items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly Lazy<PrintableReadOnlyCollection<T>> _lazyPrintableCollection = new(() => items.AsPrintable());

    /// <inheritdoc />
    public override string ToString() => _lazyPrintableCollection.Value.ToString();
}