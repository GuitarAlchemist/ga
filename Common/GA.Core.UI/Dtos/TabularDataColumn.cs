namespace GA.Core.UI.Dtos;

public record TabularDataColumn(string Name, string? DataType = null)
{
    public override string ToString()
    {
        return DataType != null ? $"{Name} ({DataType})" : Name;
    }
}
