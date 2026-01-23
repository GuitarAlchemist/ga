namespace GA.Business.ML.Rag;

using System.Collections.Generic;

/// <summary>
/// Defines the different partitions of the knowledge base.
/// </summary>
public enum KnowledgeType
{
    /// <summary>Harmonic functions, theory concepts, substitutions.</summary>
    Theory,
    
    /// <summary>Voicings, ergonomics, doigtés, playing techniques.</summary>
    Technique,
    
    /// <summary>Riffs, tabs, MIDI examples, transcriptions.</summary>
    Corpus,
    
    /// <summary>OPTIC-K invariants, hard heuristics, musical constraints.</summary>
    Rules
}

/// <summary>
/// A single item retrieved from the RAG knowledge base.
/// </summary>
public record RagResult(
    string Id,
    string Content,
    float Score,
    KnowledgeType Type,
    string Title = "",
    string SourceUrl = "",
    IReadOnlyDictionary<string, object>? Metadata = null);

/// <summary>
/// Aggregated response from multiple RAG partitions.
/// </summary>
public record PartitionedRagResponse(
    string Query,
    IReadOnlyList<RagResult> Results,
    IReadOnlyDictionary<KnowledgeType, int> PartitionCounts);

/// <summary>
/// Structured representation of a musical query, supporting a DSL-like approach
/// for more precise knowledge retrieval.
/// </summary>
public record StructuredMusicalQuery(
    string RawQuery,
    IReadOnlyList<string> Chords,
    IReadOnlyList<string> Scales,
    IReadOnlyList<string> Techniques,
    IReadOnlyList<KnowledgeType> RecommendedPartitions);
