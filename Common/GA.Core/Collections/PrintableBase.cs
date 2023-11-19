namespace GA.Core.Collections;

public abstract class PrintableBase<T>(IEnumerable<T> items,
    string? itemFormat = null,
    IFormatProvider? itemFormatProvider = null,
    string? itemSeparator = " ")
{
    private readonly IEnumerable<T> _items = items ?? throw new ArgumentNullException(nameof(items));

    public override string ToString()
    {
        return string.Join(itemSeparator ?? " ", _items.Select(PrintItem()));

        Func<T, string> PrintItem() => arg => arg is IFormattable f ? f.ToString(itemFormat, itemFormatProvider) : arg?.ToString() ?? string.Empty;
    }
}