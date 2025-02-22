using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

public class ScaleDocument : RagDocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }

    [BsonElement("intervals")]
    public required List<string> Intervals { get; set; }

    [BsonElement("intervalClassVector")]
    public required string IntervalClassVector { get; set; }

    [BsonElement("isModal")]
    public required bool IsModal { get; set; }

    [BsonElement("modalFamily")]
    public string? ModalFamily { get; set; }

    [BsonElement("isNormalForm")]
    public required bool IsNormalForm { get; set; }

    [BsonElement("isClusterFree")]
    public required bool IsClusterFree { get; set; }

    [BsonElement("scaleVideoUrl")]
    public string? ScaleVideoUrl { get; set; }

    [BsonElement("scalePageUrl")]
    public required string ScalePageUrl { get; set; }

    [BsonElement("modes")]
    public List<string>? Modes { get; set; }

    public override void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Name,
            string.Join(" ", Notes),
            string.Join(" ", Intervals),
            IntervalClassVector,
            ModalFamily ?? string.Empty,
            string.Join(" ", Modes ?? []),
            Description ?? string.Empty,
            Usage ?? string.Empty,
            string.Join(" ", Tags)
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}
