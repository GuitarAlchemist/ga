namespace GA.Data.MongoDB.Models;

public class IntervalDocument : DocumentBase
{
    public required string Name { get; set; }
    public required int Semitones { get; set; }
    public required string Quality { get; set; }
    public required int Size { get; set; }
    public bool IsCompound { get; set; }
}