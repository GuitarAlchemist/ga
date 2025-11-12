namespace GA.Data.MongoDB.Models.Rag;

using References;

/// <summary>
/// RAG document for YouTube video transcripts
/// Stores processed transcript data with embeddings for semantic search
/// </summary>
[PublicAPI]
public sealed record YouTubeTranscriptRagDocument : RagDocumentBase
{
    /// <summary>
    /// YouTube video ID
    /// </summary>
    public required string VideoId { get; init; }

    /// <summary>
    /// Video title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Channel name
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// Video URL
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Full transcript text
    /// </summary>
    public required string Transcript { get; init; }

    /// <summary>
    /// AI-generated summary (Stage 1)
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Extracted chord progressions (Stage 2)
    /// </summary>
    public List<ProgressionReference> ChordProgressions { get; set; } = [];

    /// <summary>
    /// Extracted scales (Stage 2)
    /// </summary>
    public List<ScaleReference> Scales { get; set; } = [];

    /// <summary>
    /// Extracted techniques (Stage 2)
    /// </summary>
    public List<string> Techniques { get; set; } = [];

    /// <summary>
    /// Extracted music theory concepts (Stage 2)
    /// </summary>
    public List<string> TheoryConcepts { get; set; } = [];

    /// <summary>
    /// Key topics covered in the video (Stage 1)
    /// </summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>
    /// Difficulty level (Beginner, Intermediate, Advanced)
    /// </summary>
    public string DifficultyLevel { get; set; } = "Unknown";

    /// <summary>
    /// Video duration in seconds
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Quality score from autonomous curation
    /// </summary>
    public float QualityScore { get; set; }

    /// <summary>
    /// Knowledge gap this video fills
    /// </summary>
    public string? KnowledgeGap { get; set; }

    /// <summary>
    /// Generate search text for embedding generation
    /// </summary>
    public override void GenerateSearchText()
    {
        // Combine all relevant text fields for embedding
        var parts = new List<string>
        {
            $"Title: {Title}",
            $"Channel: {Channel}",
            $"Summary: {Summary}"
        };

        if (Topics.Count > 0)
        {
            parts.Add($"Topics: {string.Join(", ", Topics)}");
        }

        if (ChordProgressions.Count > 0)
        {
            parts.Add($"Chord Progressions: {string.Join(", ", ChordProgressions.Select(p => p.Name))}");
        }

        if (Scales.Count > 0)
        {
            parts.Add($"Scales: {string.Join(", ", Scales.Select(s => s.Name))}");
        }

        if (Techniques.Count > 0)
        {
            parts.Add($"Techniques: {string.Join(", ", Techniques)}");
        }

        if (TheoryConcepts.Count > 0)
        {
            parts.Add($"Theory Concepts: {string.Join(", ", TheoryConcepts)}");
        }

        SearchText = string.Join("\n", parts);
    }
}

