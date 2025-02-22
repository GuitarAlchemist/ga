namespace GA.Data.MongoDB.Models.Rag;

using global::MongoDB.Bson.Serialization.Attributes;

public class ChordRagDocument : RagDocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("root")]
    public required string Root { get; set; }

    [BsonElement("quality")]
    public required string Quality { get; set; }

    // Denormalized relationships (high value for RAG)
    [BsonElement("relatedScales")]
    public List<ScaleReference> RelatedScales { get; set; } = [];

    [BsonElement("commonProgressions")]
    public List<ProgressionReference> CommonProgressions { get; set; } = [];

    [BsonElement("commonVoicings")]
    public List<VoicingReference> CommonVoicings { get; set; } = [];

    // Technical details (can be filtered for RAG)
    [BsonElement("intervals")]
    public required List<string> Intervals { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }
}

// Reference types for denormalization
public record ScaleReference(string Name, string Category, List<string> Notes);
public record ProgressionReference(string Name, List<string> Chords);
public record VoicingReference(string Name, List<string> Notes, string Instrument);