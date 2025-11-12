namespace GA.Data.MongoDB.Models.Rag;

/// <summary>
/// RAG document for music theory knowledge base
/// Stores chord-scale relationships, voice leading rules, harmonic progressions, and theory concepts
/// </summary>
public record MusicTheoryRagDocument : RagDocumentBase
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // "pdf", "markdown", "text", "youtube"
    public string? SourceUrl { get; set; }
    
    // Extracted music theory knowledge
    public List<string> ChordScaleRelationships { get; set; } = [];
    public List<string> VoiceLeadingRules { get; set; } = [];
    public List<string> HarmonicProgressions { get; set; } = [];
    public List<string> ChordSubstitutions { get; set; } = [];
    public List<string> ModalTheory { get; set; } = [];
    public List<string> JazzHarmonyConcepts { get; set; } = [];
    public List<string> FunctionalHarmony { get; set; } = [];
    public List<string> TheoreticalConcepts { get; set; } = [];
    public List<string> KeyInsights { get; set; } = [];
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public override void GenerateSearchText()
    {
        // Combine all relevant fields for semantic search
        var searchParts = new List<string>
        {
            Title,
            Summary,
            string.Join(", ", ChordScaleRelationships),
            string.Join(", ", VoiceLeadingRules),
            string.Join(", ", HarmonicProgressions),
            string.Join(", ", ChordSubstitutions),
            string.Join(", ", ModalTheory),
            string.Join(", ", JazzHarmonyConcepts),
            string.Join(", ", FunctionalHarmony),
            string.Join(", ", TheoreticalConcepts),
            string.Join(", ", KeyInsights)
        };

        SearchText = string.Join(" | ", searchParts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}

