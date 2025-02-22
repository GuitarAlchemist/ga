namespace GA.Data.MongoDB.Models;

public class KeyDocument : DocumentBase
{
    public required string Name { get; set; }
    public required string Root { get; set; }
    public required string Mode { get; set; } // Major, Minor
    public required List<string> AccidentedNotes { get; set; }
    public required int NumberOfAccidentals { get; set; }
}