using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

public class NoteDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }
    
    [BsonElement("midiNumber")]
    public required int MidiNumber { get; set; }
    
    [BsonElement("category")]
    public required string Category { get; set; } // Natural, Sharp, Flat, etc.
    
    [BsonElement("alias")]
    public string? Alias { get; set; }
}