namespace GA.Business.Core.Tabs;

using System.Collections.Generic;
using GA.Business.Core.Fretboard.Voicings.Search;

/// <summary>
/// Represents a labeled harmonic progression for ML training.
/// </summary>
public class ProgressionCorpusItem
{
    public string Id { get; set; } = string.Empty;
    public string StyleLabel { get; set; } = string.Empty; // "Jazz", "Rock", "Blues", etc.
    public List<VoicingDocument> Chords { get; set; } = new();
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public interface IProgressionCorpusRepository
{
    Task SaveAsync(ProgressionCorpusItem item);
    Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style);
    Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync();
    Task<long> CountAsync();
}
