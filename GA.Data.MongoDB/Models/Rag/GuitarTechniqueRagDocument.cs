namespace GA.Data.MongoDB.Models.Rag;

/// <summary>
/// RAG document for guitar technique library
/// Stores fingering patterns, exercises, chord voicings, and technique descriptions
/// </summary>
public record GuitarTechniqueRagDocument : RagDocumentBase
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // "pdf", "markdown", "text", "youtube"
    public string? SourceUrl { get; set; }
    
    // Extracted guitar-specific knowledge
    public List<string> FingeringPatterns { get; set; } = [];
    public List<string> Exercises { get; set; } = [];
    public List<string> ChordVoicings { get; set; } = [];
    public List<string> Techniques { get; set; } = [];
    public List<string> Progressions { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public List<string> FretboardPositions { get; set; } = [];
    public List<string> KeyInsights { get; set; } = [];
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public override void GenerateSearchText()
    {
        // Combine all relevant fields for semantic search
        var searchParts = new List<string>
        {
            Title,
            Summary,
            string.Join(", ", FingeringPatterns),
            string.Join(", ", Exercises),
            string.Join(", ", ChordVoicings),
            string.Join(", ", Techniques),
            string.Join(", ", Progressions),
            string.Join(", ", Styles),
            string.Join(", ", FretboardPositions),
            string.Join(", ", KeyInsights)
        };

        SearchText = string.Join(" | ", searchParts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}

