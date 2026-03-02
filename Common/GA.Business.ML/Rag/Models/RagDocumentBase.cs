namespace GA.Business.ML.Rag.Models;

using GA.Domain.Core.Design.Persistence;

/// <summary>
/// Base class for documents used in RAG (Retrieval Augmented Generation) workflows.
/// </summary>
public abstract record RagDocumentBase : DocumentBase
{
    /// <summary>
    /// Vector embedding.
    /// Note: For OPTIC-K musical vectors, this should be 216 dimensions.
    /// For Semantic Text (MiniLM), this should be 384 dimensions.
    /// See .agent/skills/optic-k-schema-guardian/SKILL.md
    /// </summary>
    public float[] Embedding { get; init; } = [];
    public string SearchText { get; protected set; } = string.Empty;

    /// <summary>
    /// Generates a robust text representation of the document for semantic embedding.
    /// </summary>
    public abstract string ToEmbeddingString();
}
