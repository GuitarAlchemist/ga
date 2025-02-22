namespace GA.Data.MongoDB.Models;

public class PitchClassDocument : DocumentBase
{
    public required int Value { get; set; }
    public required List<string> Notes { get; set; }
}