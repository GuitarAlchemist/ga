using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class SetClassDocument : DocumentBase
{
    [BsonElement("cardinality")]
    public required int Cardinality { get; set; }

    [BsonElement("intervalClassVector")]
    public required string IntervalClassVector { get; set; }

    [BsonElement("primeFormId")]
    public required int PrimeFormId { get; set; }

    [BsonElement("isModal")]
    public required bool IsModal { get; set; }

    [BsonElement("modalFamily")]
    public string? ModalFamily { get; set; }
}