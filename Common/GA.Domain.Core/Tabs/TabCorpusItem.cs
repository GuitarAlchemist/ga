namespace GA.Domain.Core.Tabs;

using System.Collections.Generic;

public class TabCorpusItem 
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty; // e.g. "guitarset"
    public string ExternalId { get; set; } = string.Empty; // Filename or ID in source
    public string Content { get; set; } = string.Empty; // Raw text or JSON
    public string Format { get; set; } = string.Empty; // "ASCII", "GuitarPro-Token", "JAMS"
    public Dictionary<string, string> Metadata { get; set; } = new();
}
