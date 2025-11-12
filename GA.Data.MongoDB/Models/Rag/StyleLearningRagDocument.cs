namespace GA.Data.MongoDB.Models.Rag;

/// <summary>
/// RAG document for style learning library
/// Stores artist/style characteristics, progressions, voicings, and techniques
/// </summary>
public record StyleLearningRagDocument : RagDocumentBase
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ArtistOrStyle { get; set; } = string.Empty; // e.g., "Wes Montgomery", "Bill Evans", "Bebop", "Cool Jazz"
    public string SourceType { get; set; } = string.Empty; // "transcription", "analysis", "youtube", "pdf"
    public string? SourceUrl { get; set; }
    
    // Extracted style characteristics
    public List<string> CharacteristicProgressions { get; set; } = [];
    public List<string> SignatureVoicings { get; set; } = [];
    public List<string> MelodicPatterns { get; set; } = [];
    public List<string> RhythmicCharacteristics { get; set; } = [];
    public List<string> HarmonicTechniques { get; set; } = [];
    public List<string> PlayingTechniques { get; set; } = [];
    public List<string> TonalPreferences { get; set; } = [];
    public List<string> StylisticInfluences { get; set; } = [];
    public List<string> KeyInsights { get; set; } = [];
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public override void GenerateSearchText()
    {
        // Combine all relevant fields for semantic search
        var searchParts = new List<string>
        {
            Title,
            Summary,
            ArtistOrStyle,
            string.Join(", ", CharacteristicProgressions),
            string.Join(", ", SignatureVoicings),
            string.Join(", ", MelodicPatterns),
            string.Join(", ", RhythmicCharacteristics),
            string.Join(", ", HarmonicTechniques),
            string.Join(", ", PlayingTechniques),
            string.Join(", ", TonalPreferences),
            string.Join(", ", StylisticInfluences),
            string.Join(", ", KeyInsights)
        };

        SearchText = string.Join(" | ", searchParts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}

