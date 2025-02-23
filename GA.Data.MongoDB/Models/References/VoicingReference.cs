namespace GA.Data.MongoDB.Models.References;

[PublicAPI]
public record VoicingReference(string Name, List<string> Notes, string Instrument);