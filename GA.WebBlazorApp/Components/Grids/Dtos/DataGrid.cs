namespace GA.WebBlazorApp.Components.Grids.Dtos;

public class DataGrid
{
    public DataGrid(
        List<DataGridColumn> columns, 
        List<DataGridRow> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    /// <summary>
    /// Gets the collection of <see cref="DataGridColumn"/>
    /// </summary>
    public List<DataGridColumn> Columns { get; init; }

    /// <summary>
    /// Gets the collection of <see cref="Dictionary{String,Object}"/>
    /// </summary>
    public List<DataGridRow> Rows { get; init; }
}