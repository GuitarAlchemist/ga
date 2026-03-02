namespace GA.Domain.Core.Theory.Tabs;

using Design.Persistence;

/// <summary>
/// Represents a raw tab file in the corpus.
/// </summary>
public sealed record TabCorpusItem : DocumentBase
{
    public required string Content { get; init; } // Full ASCII text
    public required string SourceId { get; init; } // Original file path or URL
    public required string ExternalId { get; init; } // ID in source system
    public required string Format { get; init; } // e.g. "txt", "gp5"
}
