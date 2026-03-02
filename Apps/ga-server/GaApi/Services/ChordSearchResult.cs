namespace GaApi.Services;

public class ChordSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string StackingType { get; set; } = string.Empty;
    public int NoteCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
}
