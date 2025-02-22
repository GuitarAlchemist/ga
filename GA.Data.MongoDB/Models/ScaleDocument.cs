namespace GA.Data.MongoDB.Models;

public class ScaleDocument : DocumentBase
{
    public required string Name { get; set; }
    public required List<string> Intervals { get; set; }
    public List<string>? Modes { get; set; }
}