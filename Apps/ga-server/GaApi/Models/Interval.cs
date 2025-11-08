namespace GaApi.Models;

using MongoDB.Bson.Serialization.Attributes;

public class Interval
{
    [BsonElement("Semitones")] public int Semitones { get; set; }

    [BsonElement("Function")] public string Function { get; set; } = string.Empty;

    [BsonElement("IsEssential")] public bool IsEssential { get; set; }
}
