namespace GA.WebBlazorApp.Components.Grids.Dtos;

public class DataGridColumn
{
    public string Name { get; set; }
    public string DataType { get; set; }

    public override string ToString() => $"{Name} ({DataType})";
}