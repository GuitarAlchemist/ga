namespace GA.Core.Collections;

using Extensions;

/// <summary>
///     Base class for lazy-printable read-only collections
/// </summary>
/// <typeparam name="T">The item type</typeparam>
public abstract class LazyPrintableCollectionBase<T>(IReadOnlyCollection<T> items) : IReadOnlyCollection<T>
    where T : notnull
{
    private readonly Lazy<PrintableReadOnlyCollection<T>> _lazyPrintableCollection = new(() => items.AsPrintable());

    protected readonly IReadOnlyCollection<T> Items = items ?? throw new ArgumentNullException(nameof(items));

    /// <inheritdoc />
    public override string ToString()
    {
        return _lazyPrintableCollection.Value.Count != 0
            ? _lazyPrintableCollection.Value.ToString()
            : "(Empty)";
    }

    #region IReadOnlyCollection Members

    public IEnumerator<T> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    public int Count => Items.Count;

    #endregion
}
