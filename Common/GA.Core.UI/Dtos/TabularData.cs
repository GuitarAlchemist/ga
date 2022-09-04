namespace GA.Core.UI.Dtos;

public class TabularData
{
    public TabularData(
        IReadOnlyCollection<TabularDataColumn> columns,
        IEnumerable<TabularDataRow> rows)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    public IReadOnlyCollection<TabularDataColumn> Columns { get; }

    public IEnumerable<TabularDataRow> Rows { get; }
}