namespace GA.Core.UI.Dtos;

public class TabularData(IReadOnlyCollection<TabularDataColumn> columns,
    IEnumerable<TabularDataRow> rows)
{
    public IReadOnlyCollection<TabularDataColumn> Columns { get; } = columns ?? throw new ArgumentNullException(nameof(columns));

    public IEnumerable<TabularDataRow> Rows { get; } = rows ?? throw new ArgumentNullException(nameof(rows));
}