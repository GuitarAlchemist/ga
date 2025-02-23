namespace GA.Data.MongoDB.Models.Rag;

using References;

[PublicAPI]
public sealed record ChordRagDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    public required string Quality { get; init; }

    // Denormalized relationships (high value for RAG)
    public List<ScaleReference> RelatedScales { get; init; } = [];
    public List<ProgressionReference> CommonProgressions { get; init; } = [];
    public List<VoicingReference> CommonVoicings { get; init; } = [];

    // Technical details (can be filtered for RAG)
    public required List<string> Intervals { get; init; }
    public required List<string> Notes { get; init; }
}
