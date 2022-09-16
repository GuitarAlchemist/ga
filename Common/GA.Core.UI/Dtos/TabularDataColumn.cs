namespace GA.Core.UI.Dtos;

public record TabularDataColumn(string Name, string? DataType = null)
{
    public override string ToString() => DataType != null ? $"{Name} ({DataType})" : Name;
}