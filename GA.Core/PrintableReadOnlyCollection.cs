namespace GA.Core;

public class PrintableReadOnlyCollection<T> : IReadOnlyCollection<T>
    where T : notnull
{
    private readonly IReadOnlyCollection<T> _items;
    private readonly string? _itemFormat;
    private readonly IFormatProvider? _itemFormatProvider;

    public PrintableReadOnlyCollection(
        IReadOnlyCollection<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemFormat = itemFormat;
        _itemFormatProvider = itemFormatProvider;
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        return string.Join(" ", _items.Select(PrintItem()));

        Func<T, string?> PrintItem()
        {
            return arg =>
            {
                ArgumentNullException.ThrowIfNull(arg);

                if (arg is IFormattable f)
                {
                    var result = f.ToString(_itemFormat, _itemFormatProvider);
                    return result;
                }
                else
                {
                    var result = arg.ToString();
                    return result;
                }
            };
        }
    }
    public int Count => _items.Count;
}