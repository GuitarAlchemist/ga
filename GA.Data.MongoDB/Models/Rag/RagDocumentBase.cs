namespace GA.Data.MongoDB.Models.Rag;

public abstract record RagDocumentBase : DocumentBase
{
    public float[] Embedding { get; set; } = [];
    public string SearchText { get; set; } = string.Empty;

    public abstract string ToEmbeddingString();

    public void GenerateSearchText()
    {
        SearchText = ToEmbeddingString();
    }
}
