namespace GA.Data.MongoDB.Models.Rag;

[PublicAPI]
public abstract record RagDocumentBase : DocumentBase
{
    public string? Description { get; init; }
    public string? Usage { get; init; }
    public string SearchText { get; set; } = string.Empty;
    public List<float>? Embedding { get; set; }
    public List<string> Tags { get; init; } = [];

    protected RagDocumentBase() {}

    public virtual void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Description ?? string.Empty,
            Usage ?? string.Empty,
            string.Join(" ", Tags)
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}