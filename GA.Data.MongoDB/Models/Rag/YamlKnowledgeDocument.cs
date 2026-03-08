namespace GA.Data.MongoDB.Models.Rag;

/// <summary>
/// RAG document produced from a content YAML file in GA.Business.Config.
/// Each instance represents one named entry (technique, progression, concept…)
/// flattened to a searchable text chunk and embedded for vector retrieval.
/// </summary>
public record YamlKnowledgeDocument : RagDocumentBase
{
    /// <summary>Name of the entry as declared in the YAML (e.g. "Sweep Picking").</summary>
    public required string EntryName { get; init; }

    /// <summary>Flattened, human-readable text of all YAML fields for this entry.</summary>
    public required string Content { get; init; }

    /// <summary>Category field from the YAML (e.g. "Jazz", "Lead Guitar").</summary>
    public required string Category { get; init; }

    /// <summary>Source filename without extension (e.g. "GuitarTechniques").</summary>
    public required string SourceFile { get; init; }

    /// <summary>Tags derived from Category and SourceFile.</summary>
    public List<string> Tags { get; init; } = [];

    public override string ToEmbeddingString() =>
        string.Join(" | ", new[] { EntryName, Category, Content }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
