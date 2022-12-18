namespace GA.Core.Collections;

public abstract class PrintableBase<T>
{
    private readonly IEnumerable<T> _items;
    private readonly string? _itemFormat;
    private readonly IFormatProvider? _itemFormatProvider;
    private readonly string? _itemSeparator;

    protected PrintableBase(
        IEnumerable<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null,
        string? itemSeparator = " ")
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemFormat = itemFormat;
        _itemFormatProvider = itemFormatProvider;
        _itemSeparator = itemSeparator;
    }

    public override string ToString()
    {
        return string.Join(_itemSeparator ?? " ", _items.Select(PrintItem()));

        Func<T, string> PrintItem() => arg => arg is IFormattable f ? f.ToString(_itemFormat, _itemFormatProvider) : arg?.ToString() ?? string.Empty;
    }
}