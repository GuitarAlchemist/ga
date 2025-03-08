namespace GA.Data.MongoDB.Models.Rag;

public abstract record RagDocumentBase : DocumentBase
{
    public float[] Embedding { get; set; } = [];
    public string SearchText { get; protected set; } = string.Empty;

    public virtual void GenerateSearchText()
    {
        // Override in derived classes to generate search text
        // This will be used to generate embeddings
    }
}