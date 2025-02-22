namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class PitchClassDocument : DocumentBase
{
    public required int Value { get; set; }
    public required List<string> Notes { get; set; }
}