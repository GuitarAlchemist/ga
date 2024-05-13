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

    public IEnumerator<T> GetEnumerator() =>Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
    public int Count => Items.Count;

    #endregion
    
    protected readonly IReadOnlyCollection<T> Items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly Lazy<PrintableReadOnlyCollection<T>> _lazyPrintableCollection = new(() => items.AsPrintable());

    /// <inheritdoc />
    public override string ToString() =>
        _lazyPrintableCollection.Value.Count != 0
            ? _lazyPrintableCollection.Value.ToString() 
            : "(Empty)";
}