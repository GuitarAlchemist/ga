namespace GA.Data.MongoDB.Models;

using global::MongoDB.Bson.Serialization.Attributes;

[PublicAPI]
public abstract class RagDocumentBase : DocumentBase
{
    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("usage")]
    public string? Usage { get; set; }

    [BsonElement("searchText")]
    public string SearchText { get; set; } = string.Empty;

    [BsonElement("embedding")]
    public List<float>? Embedding { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

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