using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class ChordDocument : RagDocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("root")]
    public required string Root { get; set; }

    [BsonElement("quality")]
    public required string Quality { get; set; } // Major, Minor, Diminished, etc.

    [BsonElement("intervals")]
    public required List<string> Intervals { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }

    [BsonElement("relatedScales")]
    public List<RelatedScale> RelatedScales { get; set; } = [];

    [BsonElement("commonProgressions")]
    public List<RelatedProgression> CommonProgressions { get; set; } = [];

    public override void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Name,
            Root,
            Quality,
            string.Join(" ", Notes),
            string.Join(" ", RelatedScales.Select(s => s.Name)),
            string.Join(" ", CommonProgressions.Select(p => p.Name)),
            Description ?? string.Empty,
            Usage ?? string.Empty,
            string.Join(" ", Tags)
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}

public record RelatedScale(string Name, List<string> Notes);
public record RelatedProgression(string Name, List<string> Chords);
